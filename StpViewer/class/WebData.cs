using System.Collections;
using System.Collections.Generic;
using WebSocketSharp;
using System;



public class WebData
{
    /// <summary>  
    /// The WebSocket address to connect  
    /// </summary>  
    public string address = "ws://127.0.0.1:1822";

    /// <summary>  
    /// Default text to send  
    /// </summary>  
    private string _msgToSend = "Hello World!";

    /// <summary>  
    /// Debug text to draw on the gui  
    /// </summary>  
    private string _text = string.Empty;

    /// <summary>  
    /// Saved WebSocket instance  
    /// </summary>  
    public WebSocket _webSocket;

    //private Queue<DataInfo> _msgQueue = new Queue<DataInfo>();
    private Queue<string> _msgQueue = new Queue<string>();
    private Queue<byte[]> _binQueue = new Queue<byte[]>();
    //public Queue<DataInfo> MsgQueue { get { return _msgQueue; } }
    public Queue<string> MsgQueue { get { return _msgQueue; } }
    public Queue<byte[]> BinQueue { get { return _binQueue; } }
    public Queue<string> msgOutQueue = new Queue<string>();
    public WebSocket WebSocket { get { return _webSocket; } }
    public string Address { get { return address; } }
    public bool isOpen { get { return _webSocket!=null &&_webSocket.IsAlive; } }
    public string Text { get { return _text; } }
    
    public List<float> pqDataFromBotServer = new List<float>();
    public WebData(string url)
    {
        address = url;
    }
    

    public void OpenWebSocket()
    {
        if (_webSocket == null)
        {
            // Create the WebSocket instance  
            _webSocket = new WebSocket(address);
           _webSocket.OnError += (sender, e) => {
               Console.Write("OnError:"+ e.Message);
   
            };
            _webSocket.OnClose += (sender, e) => {
                Console.Write("Closed because:"+e.Reason);

            };
            _webSocket.OnOpen += (sender, e) =>
            {
                Console.Write("open");
            };
            _webSocket.OnMessage += (sender, e) =>
            {
                if (e.IsText)
                {
                    _msgQueue.Enqueue(e.Data);
                }
                else if (e.IsBinary)
                {
                    _binQueue.Enqueue(e.RawData);
                }
            };
            _webSocket.Connect();

            
        }
    }

    
}

