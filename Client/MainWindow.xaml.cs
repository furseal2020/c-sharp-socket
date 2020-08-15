using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
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

using System.Net.Sockets;
using System.Threading;
using Newtonsoft.Json;

namespace Client
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public Socket client;
        String client_username;
        bool flag;

        public MainWindow()
        {
            InitializeComponent();
            client_username = "";
            flag = false; //按下disconnect時需跳出Disconnect_listen()
            
        }

        private void Btn_connect_Click(object sender, RoutedEventArgs e)
        {
            String action = (string)btn_connect.Content;
            

            if (action == "Connect")
            {
                String host = txt_socket_ip.Text;
                int port = Convert.ToInt32(txt_socket_port.Text);
                client_username = txt_username.Text;

                //connect to socket server
                IPAddress ip = IPAddress.Parse(host);

                //socket()
                client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                try
                {
                    //connect()
                    client.Connect(new IPEndPoint(ip, port));

                    btn_connect.Content = "Disconnect";
                    Thread receiveThread = new Thread(ReceiveMessage);
                    receiveThread.Start();
                  
                    Thread thread2 = new Thread(Disconnect_listen);
                    flag = true;
                    thread2.Start();
                    
                    
                }
                catch (Exception)
                {
                    txt_msg_region.Text += "Server is not alive.\n";
                }
            }
            else //action=="Disconnect"
            {
                btn_connect.Content = "Connect";
                try
                {
                    client.Shutdown(SocketShutdown.Both);
                    client.Close();
                }
                catch (Exception)
                { 
                    
                }
                txt_msg_region.Text += "Disconnect Success.\n";
                flag = false;
                txt_server_status.Text = "Server is not connected.";
                
            }

        }

        //to know whether any disconnection occurs
        private void Disconnect_listen()
        {
            while (flag==true)
            {
                if (SocketExtensions.IsConnected(client))
                {
                    txt_server_status.Dispatcher.BeginInvoke(
                           new Action(() => { txt_server_status.Text = "Server is connected."; }), null);
                }
                else
                {
                    
                    txt_server_status.Dispatcher.BeginInvoke(
                           new Action(() => { txt_server_status.Text = "Server is not connected."; }), null);
                }

            }
        }

        public void ReceiveMessage()
        {
            while (true)
            {
                try
                {
                    byte[] result = new byte[1024];
                    int receiveNumber = client.Receive(result);
                    String recStr = Encoding.ASCII.GetString(result, 0, receiveNumber);

                    if (receiveNumber > 0)
                    {
                        Message receive_msg = JsonConvert.DeserializeObject<Message>(recStr);
                        String display_name = receive_msg.Username;
                        String display_msg = receive_msg.Msg;
                        if (display_name == "server_request_username")
                        {

                            try
                            {
                                //send username to server
                                Message msg = new Message();
                                msg.Username = "client_response_username";
                                msg.Msg = client_username;
                                string json = JsonConvert.SerializeObject(msg);
                                client.Send(Encoding.ASCII.GetBytes(json));

                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine(ex);
                                txt_msg_region.Text += "Send Fail.\n";
                            }
                        }
                        else if (display_name == "server_reply" || display_name == "server_welcome" || display_name == "server_broadcast")
                        {
                            txt_msg_region.Dispatcher.BeginInvoke(
                                 new Action(() => { txt_msg_region.Text += display_msg; }), null);
                        }
                        else if (display_name == "server_stop_listen")
                        {
                            client.Shutdown(SocketShutdown.Both);
                            client.Close();
                            flag = false;
                            txt_server_status.Text = "Server is not connected.";
                        }
                        else if (display_name != null && display_msg != null)
                        {
                            txt_msg_region.Dispatcher.BeginInvoke(
                                 new Action(() => { txt_msg_region.Text += display_name + " : " + display_msg; }), null);
                        }
                        else
                        {
                            //Do nothing
                        }
                    }
                }
                catch (ObjectDisposedException)
                {
                    try
                    {
                        client.Shutdown(SocketShutdown.Both);
                        client.Close();
                    }
                    catch (Exception)
                    {
                        break;
                    }
                    break;
                }
                catch (Exception)
                {
                    //exception close()
                    try
                    {
                        client.Shutdown(SocketShutdown.Both);
                        client.Close();
                    }
                    catch (Exception)
                    {
                        break;
                    }
                    break;
                }
            }
        }

        private void Btn_send_Click(object sender, RoutedEventArgs e)
        {
            String text = txt_msg.Text;

            try
            {
                //send message to server
                Message msg = new Message();
                msg.Username = client_username;
                msg.Msg = text + "\n";
                string json = JsonConvert.SerializeObject(msg);
                client.Send(Encoding.ASCII.GetBytes(json));
                txt_msg.Text = "";
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                txt_msg_region.Text += "Send Fail.\n";
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            Environment.Exit(0);
        }
    }
}
