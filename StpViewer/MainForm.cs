using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using AnyCAD.Platform;
using AnyCAD.Exchange;
using AnyCAD.Presentation;
using System.Threading.Tasks;
using System.Threading;
using System.Xml;
using WebSocketSharp;
using RobotView;
using System.IO;


namespace StpViewer
{
    public partial class MainForm : Form
    {
       
        private AnyCAD.Presentation.RenderWindow3d renderView = null;
        private AnyRobot_U any_robot = new AnyRobot_U();
        private bool motionFinished = true;
        private bool ifUpDateSimModelFromWebSocket = false;
        private WebData _wabDataFroBotServerTransfer;
        private List<float> step = new List<float>();
        private MessageFromBotServer mess = new MessageFromBotServer();
        private string webSocketSerAddress = "127.0.0.1";
        // private string webSocketSerAddress = "10.10.10.126";
        //private string webSocketSerAddress = "10.10.10.133";
        // private string webSocketSerAddress = "10.10.10.126";
        private string webSocketServerPort = "5866";
        GroupSceneNode robot_node = new GroupSceneNode();
        List<GroupSceneNode> partNodeList = new List<GroupSceneNode>();
        List<SceneNode> geoNodeList = new List<SceneNode>();

        public MainForm()
        {
            InitializeComponent();

            this.renderView = new AnyCAD.Presentation.RenderWindow3d();
            this.renderView.Location = new System.Drawing.Point(0, 0);
            this.renderView.Size = this.Size;
            this.renderView.TabIndex = 1;
            this.splitContainer1.Panel2.Controls.Add(this.renderView);

            this.renderView.MouseClick += new System.Windows.Forms.MouseEventHandler(this.OnRenderWindow_MouseClick);

            GlobalInstance.EventListener.OnChangeCursorEvent += OnChangeCursor;
            GlobalInstance.EventListener.OnSelectElementEvent += OnSelectElement;
            //GlobalInstance.EventListener.OnSelectElementEvent += OnSelectionChanged;

            //System.Timers.Timer t = new System.Timers.Timer(10);//实例化Timer类，设置时间间隔
            //t.Elapsed += new System.Timers.ElapsedEventHandler(Update);//到达时间的时候执行事件
            //t.AutoReset = true;//设置是执行一次（false）还是一直执行(true)
            //t.Enabled = true;//是否执行System.Timers.Timer.Elapsed事件
        }

        private void OnSelectElement(SelectionChangeArgs args)
        {
            if (!args.IsHighlightMode())
            {
                SelectedShapeQuery query = new SelectedShapeQuery();
                renderView.QuerySelection(query);
                var shape = query.GetGeometry();
                if (shape != null)
                {
                    GeomCurve curve = new GeomCurve();
                    if (curve.Initialize(shape))
                    {
                        TopoShapeProperty property = new TopoShapeProperty();
                        property.SetShape(shape);
                        Console.WriteLine("Edge Length {0}", property.EdgeLength());
                    }
                }
            }
        }
        private bool m_PickPoint = false;
        private void OnRenderWindow_MouseClick(object sender, MouseEventArgs e)
        {
            if (!m_PickPoint)
                return;

            AnyCAD.Platform.PickHelper pickHelper = renderView.PickShape(e.X, e.Y);
            if (pickHelper != null)
            {
                // add a ball
                //Platform.TopoShape shape = GlobalInstance.BrepTools.MakeSphere(pt, 2);
                //renderView.ShowGeometry(shape, 100);
            }
            // Try the grid
            Vector3 pt = renderView.HitPointOnGrid(e.X, e.Y);
            if (pt != null)
            {
                //Platform.TopoShape shape = GlobalInstance.BrepTools.MakeSphere(pt, 2);
                //renderView.ShowGeometry(shape, 100);
            }
        }
        private void OnChangeCursor(String commandId, String cursorHint)
        {

            if (cursorHint == "Pan")
            {
                this.renderView.Cursor = System.Windows.Forms.Cursors.SizeAll;
            }
            else if (cursorHint == "Orbit")
            {
                this.renderView.Cursor = System.Windows.Forms.Cursors.Hand;
            }
            else if (cursorHint == "Cross")
            {
                this.renderView.Cursor = System.Windows.Forms.Cursors.Cross;
            }
            else
            {
                if (commandId == "Pick")
                {
                    this.renderView.Cursor = System.Windows.Forms.Cursors.Arrow;
                }
                else
                {
                    this.renderView.Cursor = System.Windows.Forms.Cursors.Default;
                }
            }

        }
        /// <summary>
        /// 相当于Unity的Update
        /// </summary>
        /// <param name="source"></param>
        /// <param name="e"></param>
        /// 

        void Update(object source, System.Timers.ElapsedEventArgs e)
        {
            if (motionFinished)//是否对碰撞做出反应，是否停止运动
            {
                motionFinished = false;
                if (ifUpDateSimModelFromWebSocket)
                {

                    if (_wabDataFroBotServerTransfer._webSocket!=null)
                    {

                        if (_wabDataFroBotServerTransfer.msgOutQueue.Count > 0)
                        {
                            StartUpdateSimModel();
                            //_wabDataFroBotServerTransfer.WebSocket.Send(_wabDataFroBotServerTransfer.msgOutQueue.Dequeue());
                            _wabDataFroBotServerTransfer._webSocket.Send(OperateBotCmd(_wabDataFroBotServerTransfer.msgOutQueue.Dequeue()));

                        }
                        //_wabDataFroBotServerTransfer.WebSocket.Send("1&1&0&0&0&Read --check_none");
                        _wabDataFroBotServerTransfer._webSocket.Send(OperateBotCmd("1&1&0&0&0&Read --check_none"));
                        Console.WriteLine("send Read");
                    }
                    

                    while (_wabDataFroBotServerTransfer.BinQueue.Count > 0)
                    {
                        mess.byteFromBotServer.Enqueue(_wabDataFroBotServerTransfer.BinQueue.Dequeue());
                    }
                    while (mess.byteFromBotServer.Count > 0)
                    {
                        if (step == null)
                        {
                            step = new List<float>();
                        }
                        // List<float> step = new List<float>();
                        //step.Clear();
                        step=step.Concat(mess.ProcessBiteQue(any_robot.partList.Count)).ToList();
                        //Debug.Log(step)
                        //Debug.Log();

                        
                    }
                    if (step != null && step.Count > any_robot.partList.Count*7-1)
                    {
                        
                        float[] stepData = new float[any_robot.partList.Count * 7];
                        for (int data_i = 0; data_i < stepData.Length; data_i++)
                        {
                            stepData[data_i] = step[data_i];
                        }
                        MotionWithPQ(stepData);
                        step.RemoveRange(0, any_robot.partList.Count * 7);
                        
                    }
                    renderView.RequestDraw();
                }
                motionFinished = true;
            }
            
        }
        private static byte[] OperateBotCmd(string str)
        {
            string[] strArray = str.Split(new char[] { '&' });
            if (strArray.Length > 2)
            {
                try
                {
                    int cmd_id = int.Parse(strArray[0]);
                    long cmd_option = long.Parse(strArray[1]);
                    long reserved_1 = long.Parse(strArray[2]);
                    long reserved_2 = long.Parse(strArray[3]);
                    long reserved_3 = long.Parse(strArray[4]);
                    byte[] cmdByte = Encoding.Default.GetBytes(strArray[5]);

                    List<byte> packData = new List<byte>();

                    byte[] byte_cmd_length = BitConverter.GetBytes(cmdByte.Length);
                    byte[] byte_cmd_id = BitConverter.GetBytes(cmd_id);
                    byte[] byte_cmd_option = BitConverter.GetBytes(cmd_option);
                    byte[] byte_cmd_res1 = BitConverter.GetBytes(reserved_1);
                    byte[] byte_cmd_res2 = BitConverter.GetBytes(reserved_2);
                    byte[] byte_cmd_res3 = BitConverter.GetBytes(reserved_3);
                    packData.AddRange(byte_cmd_length);
                    packData.AddRange(byte_cmd_id);
                    packData.AddRange(byte_cmd_option);
                    packData.AddRange(byte_cmd_res1);
                    packData.AddRange(byte_cmd_res2);
                    packData.AddRange(byte_cmd_res3);
                    packData.AddRange(cmdByte);

                    return packData.ToArray();
                }
                catch (Exception e)
                {
                    //Console.WriteLine(e.ToString());

                    return null;
                }

            }
            else
            {
                return null;
            }

        }
        private void StartSim(object sender, EventArgs e)
        {
            StartUpdateSimModel();
        }
        public void StartUpdateSimModel()
        {
            ifUpDateSimModelFromWebSocket = true;
            if (_wabDataFroBotServerTransfer == null|| !_wabDataFroBotServerTransfer.isOpen)
            {
                string url = "ws://" + webSocketSerAddress + ":" + webSocketServerPort;
                _wabDataFroBotServerTransfer = new WebData(url);
                _wabDataFroBotServerTransfer.OpenWebSocket();
            }

            //_wabDataFroBotServerTransfer = new WebData("ws://120.27.231.59:1822");
            if (_wabDataFroBotServerTransfer == null)
            {
                
            }

            //botCmd = "0&1&0&0&0&" + GameObject.Find("InputField").GetComponent<UnityEngine.UI.InputField>().text + " \0";
            //botCmdSend = false;
        }

        public void MotionWithPQ(List<float> pqList)
        {
            if (pqList.Count > 6)
            {
                for (int part_i = 1; part_i < any_robot.partList.Count; part_i++)
                {
                    float[] pqa = new float[7];
                    for (int i = 0; i < 7; i++)
                    {
                        pqa[i] = pqList[i + part_i * 7];
                    }
                    partNodeList[part_i].SetTransform(QuaternionToTransform(pqa));
                }
            }
            //motionFinished = true;
        }
        public void MotionWithPQ(float[] pqList)
        {
            if (pqList.Length > 6)
            {
                for (int part_i = 1; part_i < any_robot.partList.Count; part_i++)
                {
                    float[] pqa = new float[7];
                    for (int i = 0; i < 7; i++)
                    {
                        pqa[i] = pqList[i + part_i * 7];
                    }
                    partNodeList[part_i].SetTransform(QuaternionToTransform(pqa));
                }
            }
            motionFinished = true;
        }
        void OnSelectionChanged(SelectionChangeArgs args)
        {

        }

        private void MainForm_Resize(object sender, EventArgs e)
        {
            if (renderView != null)
                renderView.Size = this.splitContainer1.Panel2.Size;
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            renderView.ExecuteCommand("ShadeWithEdgeMode");
            renderView.ShowCoordinateAxis(true);
            renderView.SetPickMode((int)EnumPickMode.RF_Face);
            this.renderView.RequestDraw();
        }

        async Task Delay()
        {
            await Task.Delay(1000);
            Console.Write(11);
        }

        SceneNode node1;
        private void OpenRobotXml(object sender, EventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Filter = "XML File(*.xml;)|*.xml;|All Files(*.*)|*.*";
            if (dlg.ShowDialog() == DialogResult.OK)
            {

                any_robot.LoadRobot_Aris(dlg.FileName, true);
                int a = 0;
            }
            ShowAnyRobot();
        }

        private void ShowAnyRobot()
        {

            this.treeViewStp.Nodes.Clear();
            this.renderView.ClearScene();
            for (int part_i = 0; part_i < any_robot.partList.Count; part_i++)
            {
                GroupSceneNode onePartNode = new GroupSceneNode();
                //Console.Write(onePartNode.GetTransform().GetTranslation().ToString());
                for (int geo_i = 0; geo_i < any_robot.partList[part_i].geometryPathList.Count; geo_i++)
                {

                    TopoShape geo = GlobalInstance.BrepTools.LoadFile(new AnyCAD.Platform.Path(any_robot.partList[part_i].geometryPathList[geo_i]));
                    SceneNode oneGeoNode = renderView.ShowGeometry(geo, part_i);
                    float[] oneGeoPq = new float[7];
                    for (int i = 0; i < 7; i++)
                    {
                        oneGeoPq[i] = any_robot.partList[part_i].geometryPQ_List[i + geo_i * 7];
                        // oneGeoPq[i] = any_robot.partList[part_i].partPQ_initial[i];
                    }
                    oneGeoNode.SetTransform(QuaternionToTransform(oneGeoPq));
                    renderView.RequestDraw();
                    geoNodeList.Add(oneGeoNode);
                    onePartNode.AddNode(oneGeoNode);
                    renderView.SceneManager.RemoveNode(oneGeoNode);
                }
                float[] onePartPq = new float[7];
                for (int j = 0; j < 7; j++)
                {
                    onePartPq[j] = any_robot.partList[part_i].partPQ_initial[j];
                }
                onePartNode.SetTransform(QuaternionToTransform(onePartPq));
                renderView.SceneManager.AddNode(onePartNode);
                partNodeList.Add(onePartNode);
            }
            renderView.SceneManager.AddNode(robot_node);
            robot_node.SetPickable(false);
            renderView.RequestDraw();
            for (int k = 0; k < partNodeList.Count; k++)
            {
                Console.WriteLine("part" + k + partNodeList[k].GetTransform().GetTranslation().ToString());
            }
            for (int k = 0; k < geoNodeList.Count; k++)
            {
                Console.WriteLine("geo" + k + geoNodeList[k].GetTransform().GetTranslation().ToString());
            }
        }

        private async void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Filter = "STEP File(*.stp;*.step)|*.stp;*.step|All Files(*.*)|*.*";
            if (DialogResult.OK == dlg.ShowDialog())
            {
                //this.treeViewStp.Nodes.Clear();
                //this.renderView.ClearScene();
                TopoShape a = GlobalInstance.BrepTools.LoadFile(new AnyCAD.Platform.Path(dlg.FileName));
                node1 = renderView.ShowGeometry(a, 0);
                node1.SetPickable(true);
                
                robot_node.AddNode(node1);
                renderView.SceneManager.AddNode(robot_node);
                renderView.SceneManager.RemoveNode(node1);
            }

            renderView.SetPickMode((int)(EnumPickMode.RF_Default));
            //renderView.SetPickMode((int)(EnumPickMode.RF_Vertex));
            //OpenFileDialog dlg = new OpenFileDialog();
            //dlg.Filter = "STEP (*.stp;*.step)|*.stp;*.step|All Files(*.*)|*.*";

            //if (DialogResult.OK != dlg.ShowDialog())

            //    return;

            //AnyCAD.Exchange.ShowShapeReaderContext context = new AnyCAD.Exchange.ShowShapeReaderContext(renderView.SceneManager);
            ////context.NextShapeId = ++ shapeId;
            //AnyCAD.Exchange.StepReader reader = new AnyCAD.Exchange.StepReader();
            //reader.Read(dlg.FileName, context);

            ////shapeId = context.NextShapeId + 1;
            //renderView.RequestDraw(EnumRenderHint.RH_LoadScene);
        }

        Matrix4 QuaternionToTransform(float[] pq)
        {

            //float q0 = pq[3];
            //float q1 = pq[4] * 1;
            //float q2 = pq[5] * 1;
            //float q3 = pq[6];
            float q0 = pq[6];
            float q1 = pq[3] * 1;
            float q2 = pq[4] * 1;
            float q3 = pq[5];
            float[] rot = new float[16];
            rot[0] = 1 - 2 * q2 * q2 - 2 * q3 * q3;
            rot[1] = 2 * q1 * q2 - 2 * q0 * q3;
            rot[2] = 2 * q1 * q3 + 2 * q0 * q2;
            rot[4] = 2 * q1 * q2 + 2 * q0 * q3;
            rot[5] = 1 - 2 * q1 * q1 - 2 * q3 * q3;
            rot[6] = 2 * q2 * q3 - 2 * q0 * q1;
            rot[8] = 2 * q1 * q3 - 2 * q0 * q2;
            rot[9] = 2 * q2 * q3 + 2 * q0 * q1;
            rot[10] = 1 - 2 * q1 * q1 - 2 * q2 * q2;

            rot[3] = pq[0] * 1000;
            rot[7] = pq[1] * 1000;
            rot[11] = pq[2] * 1000;
            rot[12] = 0;
            rot[13] = 0;
            rot[14] = 0;
            rot[15] = 1;



            Matrix4 trf = new Matrix4();
            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    trf.m[i, j] = rot[i * 4 + j];
                }

            }
            //Matrix4 trf_1 = GlobalInstance.MatrixBuilder.MakeTranslate(new Vector3(pq[0], pq[1], pq[2]));
            //return GlobalInstance.MatrixBuilder.Multiply(trf, trf_1);
            return trf;
        }
        void UpdateModel(object source, System.Timers.ElapsedEventArgs e)
        {
            Matrix4 tr = robot_node.GetTransform();
            Matrix4 tr_1 = GlobalInstance.MatrixBuilder.MakeTranslate(0.1, 0.1, 0.1);
            Matrix4 tr_2 = GlobalInstance.MatrixBuilder.Multiply(tr, tr_1);
            robot_node.SetTransform(tr_2);
            //Console.WriteLine(robot_node.GetTransform().GetTranslation().ToString());

            renderView.RequestDraw();

        }
        void test()
        {
            System.Timers.Timer t = new System.Timers.Timer(100);//实例化Timer类，设置时间间隔
            t.Elapsed += new System.Timers.ElapsedEventHandler(Method2);//到达时间的时候执行事件
            t.AutoReset = true;//设置是执行一次（false）还是一直执行(true)
            t.Enabled = true;//是否执行System.Timers.Timer.Elapsed事件
        }
        void Method2(object source, System.Timers.ElapsedEventArgs e)
        {
            Console.WriteLine(DateTime.Now.ToString() + "_" + Thread.CurrentThread.ManagedThreadId.ToString());
        }

        //private void openToolStripMenuItem_Click(object sender, EventArgs e)
        //{
        //    OpenFileDialog dlg = new OpenFileDialog();
        //    dlg.Filter = "STEP File(*.stp;*.step)|*.stp;*.step|All Files(*.*)|*.*";

        //    if (DialogResult.OK == dlg.ShowDialog())
        //    {
        //        this.treeViewStp.Nodes.Clear();
        //        this.renderView.ClearScene();

        //        CADBrower browser = new CADBrower(this.treeViewStp, this.renderView);
        //        AnyCAD.Exchange.StepReader reader = new AnyCAD.Exchange.StepReader();
        //        reader.Read(dlg.FileName, browser);
        //    }
        //    renderView.FitAll();
        //}

        private void treeViewStp_AfterSelect(object sender, TreeViewEventArgs e)
        {

        }

        private void openIGESToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Filter = "IGES File(*.iges;*.igs)|*.igs;*.igesp|All Files(*.*)|*.*";

            if (DialogResult.OK == dlg.ShowDialog())
            {
                this.treeViewStp.Nodes.Clear();
                this.renderView.ClearScene();

                CADBrower browser = new CADBrower(this.treeViewStp, this.renderView);
                AnyCAD.Exchange.IgesReader reader = new AnyCAD.Exchange.IgesReader();
                reader.Read(dlg.FileName, browser);
            }

            renderView.View3d.FitAll();
        }

        private void saveImageToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveFileDialog dlg = new SaveFileDialog();
            dlg.Filter = "Image File(*.png;*.jpg)|*.png;*.jpg|All Files(*.*)|*.*";

            if (DialogResult.OK == dlg.ShowDialog())
            {
                renderView.CaptureImage(dlg.FileName);
            }
        }

        private void MainForm_Load_1(object sender, EventArgs e)
        {

        }

        private void ShowPick()
        {

        }

        private void pickToolStripMenuItem_Click(object sender, EventArgs e)
        {
            bool res= renderView.ExecuteCommand("Pick");
        }

        private void queryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SelectedShapeQuery context = new SelectedShapeQuery();
            renderView.QuerySelection(context);
            TopoShape subShape = context.GetSubGeometry();
            if (subShape!=null)
            {
                Console.WriteLine(subShape.GetShapeType());
            }
            
            //surface
            GeomSurface surface = new GeomSurface();
            if (surface.Initialize(subShape))
            {
                Console.Write("surface");
            }
            //curve
            GeomCurve curve = new GeomCurve();
            if (curve.Initialize(subShape))
            {
                Vector3 startPt = curve.D0(curve.FirstParameter());
                Vector3 pt1 = curve.GetStartPoint();
                Vector3 endPt = curve.D0(curve.LastParameter());
                Vector3 pt2 = curve.GetEndPoint();
                switch ((EnumCurveType)curve.GetCurveType())
                {
                    case EnumCurveType.CurveType_OtherCurve:
                        Console.Write("other");
                        break;
                    case EnumCurveType.CurveType_BSplineCurve:
                        break;
                    case EnumCurveType.CurveType_BezierCurve:
                        break;
                    case EnumCurveType.CurveType_Parabola:
                        break;
                    case EnumCurveType.CurveType_Hyperbola:
                        break;
                    case EnumCurveType.CurveType_Ellipse:
                        break;
                    case EnumCurveType.CurveType_Circle:
                        Console.Write("Circle");
                        break;
                    case EnumCurveType.CurveType_Line:
                        Console.Write("Line");
                        LineNode tempLineNode = new LineNode();
                        LineStyle lineStyle = new LineStyle();
                        lineStyle.SetPatternStyle((int)EnumLinePattern.LP_DashedLine);
                        lineStyle.SetColor(100, 0, 100);
                        tempLineNode.SetLineStyle(lineStyle);
                        tempLineNode.Set(new Vector3(startPt.X + 0.1, startPt.Y + 10, startPt.Z + 0.1), endPt);
                        tempLineNode.SetVisible(true);
                        renderView.SceneManager.AddNode(tempLineNode);
                        renderView.RequestDraw();
                        break;
                    default:
                        break;
                }



                ElementId id = context.GetNodeId();
                MessageBox.Show(id.AsInt().ToString());
                //...
            }

        }
    }


}
