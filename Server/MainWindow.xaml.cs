using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Newtonsoft.Json;
using System.Windows.Interop;

namespace Server
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public Socket server;
        public bool onclick;
        Dictionary<String, Socket> connectedClient; //<username,socket>
        private static readonly object key = new object(); 

        public MainWindow()
        {
            InitializeComponent();
            onclick = false; //stop listen沒有被按下去
            connectedClient = new Dictionary<String, Socket>();

        }

        private void Btn_start_Click(object sender, RoutedEventArgs e)
        {
            if (server == null)
            {
                txt_msg_region.Text = "Server Start";
                String host = txt_host_ip.Text; //localhost : 127.0.0.1
                IPAddress ip = IPAddress.Parse(host);
                //socket()
                server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                //bind()
                int port = Convert.ToInt32(txt_host_port.Text);
                server.Bind(new IPEndPoint(ip, port)); 
                //listen()
                server.Listen(10);

                Thread thread = new Thread(Listen);
                thread.Start();

                Thread thread2 = new Thread(Disconnect_listen);
                thread2.Start();
            }
        }

        //to know whether any disconnection occurs
        private void Disconnect_listen()
        {
            while (true)
            {
                String client_key = "";

                lock (key)
                {
                    foreach (var clientSocket in connectedClient)
                    {

                        if (SocketExtensions.IsConnected(clientSocket.Value))
                        {
                            //still connecting.
                        }
                        else
                        {
                            //already disconnected.
                            txt_msg_region.Dispatcher.BeginInvoke(
                                new Action(() => { txt_msg_region.Text += "\n" + clientSocket.Key + " disconnect\n"; }), null);

                            clientSocket.Value.Shutdown(SocketShutdown.Both);
                            clientSocket.Value.Close();

                            client_key = clientSocket.Key;
                            break;

                        }
                    }
                    if (client_key != "")
                    {
                        connectedClient.Remove(client_key);
                        client_key = "";
                    }

                }
            }
        }

        //listen to socket client
        private void Listen()
        {
            while (true)
            {
                if (onclick == true)
                {
                    break;
                }

                //accept()
                try
                {
                    Socket client = server.Accept();

                    Thread receive = new Thread(ReceiveMsg);
                    receive.Start(client);
                }
                catch(Exception)
                {
                    break;
                }

            }
        }

        //receive client message and send to client
        public void ReceiveMsg(object client)
        {
            String client_username="";

            Socket connection = (Socket)client;
            IPAddress clientIP = (connection.RemoteEndPoint as IPEndPoint).Address;

            //Ask for username of clientIP
            Message msg1 = new Message();
            msg1.Username = "server_request_username";
            msg1.Msg = "";
            string json1 = JsonConvert.SerializeObject(msg1);
            connection.Send(Encoding.ASCII.GetBytes(json1));

            //receive the username of the clientIP
            bool flag = false;//還沒取得username不跳出迴圈
            do
            {
                byte[] result1 = new byte[1024];
                //receive message from client
                int receive_num1 = connection.Receive(result1);
                String receive_str1 = Encoding.ASCII.GetString(result1, 0, receive_num1);
                if(receive_num1 > 0)
                { 
                    Message receive_msg1 = JsonConvert.DeserializeObject<Message>(receive_str1);
                    String display_msg1 = receive_msg1.Msg;
                    String display_name1 = receive_msg1.Username;

                    if (display_msg1!=null && display_name1!=null)
                    {              
                        if (display_name1 == "client_response_username")
                        {
                            client_username = display_msg1;
                            flag = true;
                        }
                    }
                }
            }while (flag==false);

            lock (key)
            {
                connectedClient.Add(client_username, connection);
            }
            
            txt_msg_region.Dispatcher.BeginInvoke(
                new Action(() => { txt_msg_region.Text += "\n" + client_username + "("+clientIP + ") connect\n"; }), null);

            //send welcome message to client
            Message msg2 = new Message();
            msg2.Username = "server_welcome";
            msg2.Msg = "Welcome " + client_username + "\n";
            string json2 = JsonConvert.SerializeObject(msg2);
            connection.Send(Encoding.ASCII.GetBytes(json2));
            
            while (true)
            {
                try
                {
                    byte[] result = new byte[1024];
                    //receive message from client
                    int receive_num = connection.Receive(result);
                    String receive_str = Encoding.ASCII.GetString(result, 0, receive_num);

                    if (receive_num > 0)
                    {
                        Message receive_msg = JsonConvert.DeserializeObject<Message>(receive_str);
                        String display_msg = receive_msg.Msg;
                        String display_name = receive_msg.Username;

                        if (display_msg != null && display_name != null)
                        {
                            

                            String send_str = display_name + "(" + clientIP + ") : " + display_msg;

                            //resend message to client
                            Message msg = new Message();
                            msg.Username = "server_reply";
                            msg.Msg = "You send : " + display_msg;
                            string json = JsonConvert.SerializeObject(msg);
                            connection.Send(Encoding.ASCII.GetBytes(json));

                            //broadcast to other clients
                            Message msg3 = new Message();
                            msg3.Username = "server_broadcast";
                            msg3.Msg = display_name + " : " + display_msg;
                            string json3 = JsonConvert.SerializeObject(msg3);

                            lock (key)
                            {
                                foreach (var clientSocket in connectedClient)
                                {
                                    if (clientSocket.Value != connection)
                                    {
                                        clientSocket.Value.Send(Encoding.ASCII.GetBytes(json3));
                                    }
                                }
                            }

                            txt_msg_region.Dispatcher.BeginInvoke(
                                new Action(() => { txt_msg_region.Text += send_str; }), null);

                        }
                    }
                }
                catch (Exception e)
                {
                    //exception close()
                    Console.WriteLine(e);
                    
                    /*
                    connection.Shutdown(SocketShutdown.Both);
                    connection.Close();
                    */

                    break;
                }
            }
        }

        //close() when close window
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            Environment.Exit(0);
        }

        private void Btn_listen_Click(object sender, RoutedEventArgs e) //<-目前是有問題的
        {
            if (onclick == false)
            {
                onclick = true;
                btn_listen.Content = "Start Listen";
                txt_msg_region.Text += "Server stop listening.\n";

                lock (key)
                {
                    foreach (var clientSocket in connectedClient)
                    {
                        Message msg = new Message();
                        msg.Username = "server_stop_listen";
                        msg.Msg = " ";
                        string json = JsonConvert.SerializeObject(msg);
                        clientSocket.Value.Send(Encoding.ASCII.GetBytes(json));
                        Thread.Sleep(1500);
                        try
                        {
                            clientSocket.Value.Shutdown(SocketShutdown.Both);
                            clientSocket.Value.Close();
                        }
                        catch(Exception)
                        { 
                        
                        }
                    }
                        
                    connectedClient = new Dictionary<String, Socket>();
                }
                
                server.Close();
                server = null;

            }
            else //onclick==true
            {
                onclick = false;
                btn_listen.Content = "Stop Listen";
                txt_msg_region.Text += "Server start listening.\n";

                if (server == null)
                {
                    String host = txt_host_ip.Text; //localhost : 127.0.0.1
                    IPAddress ip = IPAddress.Parse(host);
                    //socket()
                    server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    //bind()
                    int port = Convert.ToInt32(txt_host_port.Text);
                    server.Bind(new IPEndPoint(ip, port));
                    //listen()
                    server.Listen(10);

                    Thread thread = new Thread(Listen);
                    thread.Start();
                }
            }
        }
    }
}
