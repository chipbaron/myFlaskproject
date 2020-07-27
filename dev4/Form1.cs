using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO.Ports;
using System.Threading;
using System.Collections;
using System.Resources;
using System.Reflection;
using System.Net;
using System.Threading;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using Newtonsoft.Json;

namespace RfidTest
{
    public partial class Form1 : Form
    {        
        SerialPort m_comm1;
        bool bFlagComIsOpen = false;
        public const uint PRESET_VALUE = 0xFFFF;
        public const uint POLYNOMIAL = 0x8408;
        Thread WorkingThread = null;
        Socket tcpServer = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp); // TCP连接
        RFID_responseClass ResponseClass = new RFID_responseClass();
        static byte FlagCommSend=0;
        private AutoResetEvent resetEvent = new AutoResetEvent(false);
        static string strRFID_ID = "";
        byte FlagRFID_CmdInit = 0;

        public Form1()
        {
            InitializeComponent();
            InitProc();
        }

        private void InitProc()
        {            
            Control.CheckForIllegalCrossThreadCalls = false;
            button6.Enabled = false;
            IsPortOpen(); // enumerate all com port
        }
        // open com port
        private void button5_Click(object sender, EventArgs e)
        {
            string comNum="";
            try
            {
                if (comboBox1.SelectedIndex == -1)
                {
                    MessageBox.Show("没有串口可用！");
                    return;
                }
                comNum = comboBox1.SelectedItem.ToString();
                //if (!string.IsNullOrEmpty(comNum))
                //{
                //}
                if (comNum.Contains("COM"))
                {
                    m_comm1 = new SerialPort(comNum, 57600, (Parity)0, 8, (StopBits)1);
                    m_comm1.DataReceived += new SerialDataReceivedEventHandler(Comm1_DataReceived);
                    m_comm1.Open();
                    button5.Enabled = false;
                    button6.Enabled = true;
                    bFlagComIsOpen = true;
                }
            }
            catch (TimeoutException ex)         
            {
                MessageBox.Show(ex.ToString());
            }
        }

        // close com port
        private void button6_Click(object sender, EventArgs e)
        {
            if (bFlagComIsOpen)
            {
                m_comm1.Close();
                m_comm1.Dispose();
                button5.Enabled = true;
                button6.Enabled = false;
                bFlagComIsOpen = false;
            }
        }

        public bool IsPortOpen()
        {
            //create vars for testing
            bool _available = false;
            SerialPort _tempPort;
            String[] Portname = SerialPort.GetPortNames();

            //create a loop for each string in SerialPort.GetPortNames
            foreach (string str in Portname)
            {
                try
                {
                    _tempPort = new SerialPort(str);
                    _tempPort.Open();

                    //if the port exist and we can open it
                    if (_tempPort.IsOpen)
                    {
                        comboBox1.Items.Add(str);
                        _tempPort.Close();
                        _available = true;
                    }
                }

                //else we have no ports or can't open them display the 
                //precise error of why we either don't have ports or can't open them
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString(), "Error - No Ports available", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    _available = false;
                }
            }

            //return the temp bool
            return _available;
        }

        uint uiCrc16Cal(byte[] pucY, byte ucX)
        {
            byte ucI,ucJ;
            
            uint  uiCrcValue = PRESET_VALUE;
            for(ucI = 0; ucI < ucX; ucI++)
               {
                   uiCrcValue = uiCrcValue ^ pucY[ucI];
                   for(ucJ = 0; ucJ < 8; ucJ++)
                  {
                    if((uiCrcValue&0x0001) == 0x0001)
                    {
                        uiCrcValue = (uiCrcValue >> 1) ^ POLYNOMIAL;
                    }
                    else
                    {
                        uiCrcValue = (uiCrcValue >> 1);
                    }
                }
            }
            return uiCrcValue;
        }

        //-----------------------------
        // address:01-控制器1，02-控制器2
        // AntNumber: 0-天线1，1-天线2，2-天线3，3-天线4
        private void ReadRFID(byte address,byte AntNumber)
        {
            if (!bFlagComIsOpen)
                return;
            int count = 0;
            byte[] CmdSendBuf = new byte[] { 0x09, 0x01, 0x01, 0x00, 0x00, 0x00, 0x83, 0x10, 0x00, 0x00 };
            CmdSendBuf[1] = address;
            CmdSendBuf[6] = (byte)(AntNumber+0x80-1);

            uint crc_result = uiCrc16Cal(CmdSendBuf, 8);
            CmdSendBuf[8] = (byte)crc_result;
            CmdSendBuf[9] = (byte)(crc_result >> 8);

            m_comm1.Write(CmdSendBuf, 0, 10);
            //count = 0;
            //textBox1.Text = "";
        }

        private void button9_Click(object sender, EventArgs e)
        {
            ReadRFID(Convert.ToByte(textBox2.Text), Convert.ToByte(textBox3.Text));
        }

        void Comm1_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            int count, i;
            Byte[] InputBuf = new Byte[256];
            Byte[] RFIDBuf = new Byte[16];
            uint crc_result, crc_temp;        

            try
            {
                count = m_comm1.BytesToRead;
                m_comm1.Read(InputBuf, 0, count);
                if (FlagRFID_CmdInit == 55)         // init 
                {                    
                    if (InputBuf[0] == 0x11 && InputBuf[2] == 0x21) 
                    {
                        WriteLog(Convert.ToString(InputBuf[1]) + "初始化命令一OK");
                    }
                    else if (InputBuf[0] == 0x05 && InputBuf[2] == 0x3B)
                    {
                        WriteLog(Convert.ToString(InputBuf[1]) + "初始化命令二OK");
                    }
                    else if (InputBuf[0] == 0x05 && InputBuf[2] == 0x4A)
                    {
                        WriteLog(Convert.ToString(InputBuf[1]) + "初始化命令三OK");
                    }
                    else if (InputBuf[0] == 0x05 && InputBuf[2] == 0x3E)
                    {
                        WriteLog(Convert.ToString(InputBuf[1]) + "初始化命令四OK");
                    }
                    else if (InputBuf[0] == 0x05 && InputBuf[2] == 0x35)
                    {
                        WriteLog(Convert.ToString(InputBuf[1]) + "初始化命令五OK");
                    }
                    else if (InputBuf[0] == 0x05 && InputBuf[2] == 0x2F)
                    {
                        WriteLog(Convert.ToString(InputBuf[1]) + "初始化命令六OK");
                    }
                }
                //-------------------
                if (InputBuf[0] == 0x19 && InputBuf[1] <= 3 && InputBuf[2] == 0x01)
                {
                    if (uiCrc16Cal(InputBuf, 24) == ((uint)InputBuf[25] * 256 + InputBuf[24]))
                    {
                        textBox1.Text = byteToHexStr(InputBuf.Skip(7).Take(16).ToArray(),16);
                        strRFID_ID = byteToHexStr(InputBuf.Skip(7).Take(16).ToArray(), 16); ; // 保存ID
                        if (FlagCommSend == 100 || FlagCommSend==101)   // receive 1#_L Label
                        {
                            WriteLog("-01#-Left:" + strRFID_ID);
                            FlagCommSend = 111;
                            resetEvent.Set();
                        }
                        if (FlagCommSend == 102 || FlagCommSend == 103)   // receive 1#_R Label
                        {
                            WriteLog("-01#-Right:" + strRFID_ID);
                            FlagCommSend = 112;
                            resetEvent.Set();
                        }

                        if (FlagCommSend == 200 || FlagCommSend == 201)   // receive 2#_L Label
                        {
                            WriteLog("-02#-Left:" + strRFID_ID);
                            FlagCommSend = 211;
                            resetEvent.Set();
                        }
                        if (FlagCommSend == 202 || FlagCommSend == 203)   // receive 2#_R Label
                        {
                            WriteLog("-02#-Right:" + strRFID_ID);
                            FlagCommSend = 212;
                            resetEvent.Set();
                        }
                    }

                    i = 0;
                }
         
            }
            catch (TimeoutException ex)         
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private  string byteToHexStr(byte[] bytes, int length)
        {
            string returnStr = "";
            if (bytes != null)
            {
                for (int i = 0; i < length; i++)
                {
                    returnStr += bytes[i].ToString("X2");
                }
            }
            return returnStr;
        }

        // 控制器初始化
        private void button7_Click(object sender, EventArgs e)
        {
            InitRFID_Control();
        }

        private void InitRFID_Control()
        {
            int len = 0;
            int i = 0;
            //uint crc_result;
            byte TransmittingPower = 21;    // 18~22

            byte[] CmdInitRFID1_01 = new byte[] { 0x04, 0x01, 0x21, 0x01, 0x73 };
            byte[] CmdInitRFID1_02 = new byte[] { 0x08, 0x01, 0x3B, 0x01, 0x00, 0x00, 0x00, 0xCE, 0x26 };
            byte[] CmdInitRFID1_03 = new byte[] { 0x06, 0x01, 0x4A, 0x02, 0x04, 0x33, 0x17 };
            byte[] CmdInitRFID1_04 = new byte[] { 0x05, 0x01, 0x3E, 0x10, 0x99, 0x0B };
            byte[] CmdInitRFID1_05 = new byte[] { 0x05, 0x01, 0x35, 0x00, 0xB0, 0xFF };            
            //------------
            byte[] CmdInitRFID2_01 = new byte[] { 0x04, 0x02, 0x21, 0x69, 0x59 };
            byte[] CmdInitRFID2_02 = new byte[] { 0x08, 0x02, 0x3B, 0x01, 0x00, 0x00, 0x00, 0xB3, 0x2A };
            byte[] CmdInitRFID2_03 = new byte[] { 0x06, 0x02, 0x4A, 0x02, 0x04, 0xFE, 0x32 };
            byte[] CmdInitRFID2_04 = new byte[] { 0x05, 0x02, 0x3E, 0x10, 0xFD, 0xE4 };
            byte[] CmdInitRFID2_05 = new byte[] { 0x05, 0x02, 0x35, 0x00, 0xD4, 0x10 };            

            if (!bFlagComIsOpen)
                return;
            FlagRFID_CmdInit = 55; 
            //---------------------------------
            len = CmdInitRFID1_01.Length;
            m_comm1.Write(CmdInitRFID1_01, 0, len);
            Thread.Sleep(50);
            len = CmdInitRFID1_02.Length;
            m_comm1.Write(CmdInitRFID1_02, 0, len);
            Thread.Sleep(50);
            len = CmdInitRFID1_03.Length;
            m_comm1.Write(CmdInitRFID1_03, 0, len);
            Thread.Sleep(50);
            len = CmdInitRFID1_04.Length;
            m_comm1.Write(CmdInitRFID1_04, 0, len);
            Thread.Sleep(50);
            len = CmdInitRFID1_05.Length;
            m_comm1.Write(CmdInitRFID1_05, 0, len);
            Thread.Sleep(50);
            //len = CmdInitRFID1_06.Length;
            //m_comm1.Write(CmdInitRFID1_06, 0, len);
            //Thread.Sleep(50);
            AdjTransmitPower(1, TransmittingPower);
            Thread.Sleep(50);
            //-----------
            len = CmdInitRFID2_01.Length;
            m_comm1.Write(CmdInitRFID2_01, 0, len);
            Thread.Sleep(50);
            len = CmdInitRFID2_02.Length;
            m_comm1.Write(CmdInitRFID2_02, 0, len);
            Thread.Sleep(50);
            len = CmdInitRFID2_03.Length;
            m_comm1.Write(CmdInitRFID2_03, 0, len);
            Thread.Sleep(50);
            len = CmdInitRFID2_04.Length;
            m_comm1.Write(CmdInitRFID2_04, 0, len);
            Thread.Sleep(50);
            len = CmdInitRFID2_05.Length;
            m_comm1.Write(CmdInitRFID2_05, 0, len);
            Thread.Sleep(50);
            //len = CmdInitRFID2_06.Length;
            //m_comm1.Write(CmdInitRFID2_06, 0, len);
            //Thread.Sleep(50);
            AdjTransmitPower(2, TransmittingPower);
            Thread.Sleep(50);

            FlagRFID_CmdInit = 0; 
        }
        // 1#功率调整
        private int AdjTransmitPower(byte addr,byte power)
        {
            byte[] CmdInitRFIDPower = new byte[] { 0x05, 0x01, 0x2F, 0x12, 0xC2, 0xA4 };    

            if (!bFlagComIsOpen)
                return 0;

            CmdInitRFIDPower[1] = addr;
            CmdInitRFIDPower[3] = power;
            uint crc_result = uiCrc16Cal(CmdInitRFIDPower, 4);
            CmdInitRFIDPower[4] = (byte)crc_result;
            CmdInitRFIDPower[5] = (byte)(crc_result >> 8);
            m_comm1.Write(CmdInitRFIDPower, 0, 6);
            Thread.Sleep(50);
            return 1;
        }

        // 启动
        private void button8_Click(object sender, EventArgs e)
        {
            try
            {
                if (!bFlagComIsOpen)
                {
                    MessageBox.Show("请先打开串口！");
                    return ;
                }
                WorkingThread = new Thread(ReadRFIDThread);
                WorkingThread.IsBackground = true;
                WorkingThread.Start();
                //-------------------
                EndPoint point = new IPEndPoint(IPAddress.Any, 6001);
                tcpServer.Bind(point);//申请可用的IP地址和端口号
                tcpServer.Listen(10);
                button8.Enabled = false;
                //-------------------------------------
                button7.Enabled = false;
                button9.Enabled = false;
            }
            catch (System.Exception ex)
            {
                ex.ToString();
            }
        }

        private void ReadRFIDThread()
        {
            int i = 0;
            byte[] InputBuf = new byte[2048];
            byte[] RcvBuf = new byte[20];
            Byte crcChar = 0;
            string strMeter1;
            byte[] CmdSendBuf = new byte[] { 0x68, 0x10, 0x06, 0x38, 0x05, 0x01, 0x20, 0x68, 0x91, 0x08, 0x33, 0x33, 0x34, 0x33, 0x33, 0x33, 0x33, 0x33, 0x76, 0x16 };

            InitRFID_Control();  // 初始化RFID控制器
            while (true)
            {
                try
                {
                    //MessageBox.Show("开始监测客户端!");
                    Socket clientSocket = tcpServer.Accept();

                    Thread th_ReceiveData = new Thread(ReceiveData);
                    th_ReceiveData.IsBackground = true;
                    th_ReceiveData.Start(clientSocket);

                    //MessageBox.Show("Accept() OK!");

                }
                catch (System.Exception ex)
                {
                    ex.ToString();
                }

                Thread.Sleep(500);
            }
        }

        private void ReceiveData(object mySocket)
        {
            Socket tempSocket = mySocket as Socket;
            string rcvParam1, rcvParam2;
            string strTemp;
            byte[] OutputArray = new byte[1024];
            int FlagCount = 0;
            while (true)
            {
                try
                {
                    byte[] data2 = new byte[1024];
                    int length = tempSocket.Receive(data2);
                    //string str = Encoding.ASCII.GetString(data2);
                    string str = System.Text.Encoding.Default.GetString(data2);
                    if (length > 3)
                    {
                        ResponseClass = JsonConvert.DeserializeObject<RFID_responseClass>(str);
                        rcvParam1 = ResponseClass.param.Substring(0, 2);
                        rcvParam2 = ResponseClass.param.Substring(2, 1);
                        if (String.Compare(rcvParam1, "01") == 0 || String.Compare(rcvParam1, "03") == 0 || String.Compare(rcvParam1, "05") == 0)//1# RFID
                        {
                            if (String.Compare(rcvParam2, "L") == 0)
                            {
                                ReadRFID(1,0);      // read ant1
                                Thread.Sleep(50);

                                FlagCommSend = 100;
                                ReadRFID(1, 0);
                                Thread.Sleep(50);
                                if (FlagCommSend == 100)
                                {
                                    ReadRFID(1, 0);
                                    Thread.Sleep(50);
                                }
                                if (FlagCommSend == 100)
                                {
                                    ReadRFID(1, 0);
                                    Thread.Sleep(50);
                                }
                                //------
                                if (FlagCommSend == 100)   // read ant2
                                {
                                    FlagCommSend = 0;
                                    ReadRFID(1, 1);
                                    Thread.Sleep(50);

                                    FlagCommSend = 101;
                                    ReadRFID(1, 1);
                                    Thread.Sleep(50);
                                    if (FlagCommSend == 101)
                                    {
                                        ReadRFID(1, 1);
                                        Thread.Sleep(50);
                                    }
                                    if (FlagCommSend == 101)
                                    {
                                        ReadRFID(1, 1);
                                        Thread.Sleep(50);
                                    }
                                }
                                
                            }
                            else if (String.Compare(rcvParam2, "R") == 0)
                            {
                                ReadRFID(1, 2);      // read ant3
                                Thread.Sleep(50);

                                FlagCommSend = 102;
                                ReadRFID(1, 2);
                                Thread.Sleep(50);
                                if (FlagCommSend == 102)
                                {
                                    ReadRFID(1, 2);
                                    Thread.Sleep(50);
                                }
                                if (FlagCommSend == 102)
                                {
                                    ReadRFID(1, 2);
                                    Thread.Sleep(50);
                                }
                                //------
                                if (FlagCommSend == 102)   // read ant4
                                {
                                    FlagCommSend = 0;
                                    ReadRFID(1, 3);
                                    Thread.Sleep(50);

                                    FlagCommSend = 103;
                                    ReadRFID(1, 3);
                                    Thread.Sleep(50);
                                    if (FlagCommSend == 103)
                                    {
                                        ReadRFID(1, 3);
                                        Thread.Sleep(50);
                                    }
                                    if (FlagCommSend == 103)
                                    {
                                        ReadRFID(1, 3);
                                        Thread.Sleep(50);
                                    }
                                }
                            }
                        }
                        if (String.Compare(rcvParam1, "02") == 0 || String.Compare(rcvParam1, "04") == 0)   //2# RFID
                        {
                            if (String.Compare(rcvParam2, "L") == 0)
                            {
                                ReadRFID(2, 0);      // read ant1
                                Thread.Sleep(50);

                                FlagCommSend = 200;
                                ReadRFID(2, 0);
                                Thread.Sleep(50);
                                if (FlagCommSend == 200)
                                {
                                    ReadRFID(2, 0);
                                    Thread.Sleep(50);
                                }
                                if (FlagCommSend == 200)
                                {
                                    ReadRFID(2, 0);
                                    Thread.Sleep(50);
                                }
                                //------
                                if (FlagCommSend == 200)   // read ant2
                                {
                                    FlagCommSend = 0;
                                    ReadRFID(2, 1);
                                    Thread.Sleep(50);

                                    FlagCommSend = 201;
                                    ReadRFID(2, 1);
                                    Thread.Sleep(50);
                                    if (FlagCommSend == 201)
                                    {
                                        ReadRFID(2, 1);
                                        Thread.Sleep(50);
                                    }
                                    if (FlagCommSend == 201)
                                    {
                                        ReadRFID(2, 1);
                                        Thread.Sleep(50);
                                    }
                                }
                            }
                            else if (String.Compare(rcvParam2, "R") == 0)
                            {
                                ReadRFID(2, 2);      // read ant3
                                Thread.Sleep(50);

                                FlagCommSend = 202;
                                ReadRFID(2, 2);
                                Thread.Sleep(50);
                                if (FlagCommSend == 202)
                                {
                                    ReadRFID(2, 2);
                                    Thread.Sleep(50);
                                }
                                if (FlagCommSend == 202)
                                {
                                    ReadRFID(2, 2);
                                    Thread.Sleep(50);
                                }
                                //------
                                if (FlagCommSend == 202)   // read ant4
                                {
                                    FlagCommSend = 0;
                                    ReadRFID(2, 3);
                                    Thread.Sleep(50);

                                    FlagCommSend = 203;
                                    ReadRFID(2, 3);
                                    Thread.Sleep(50);
                                    if (FlagCommSend == 203)
                                    {
                                        ReadRFID(2, 3);
                                        Thread.Sleep(50);
                                    }
                                    if (FlagCommSend == 203)
                                    {
                                        ReadRFID(2, 3);
                                        Thread.Sleep(50);
                                    }
                                }
                            }                            
                        }
                        //-------------------------------------
                        resetEvent.WaitOne(100);
                        if (FlagCommSend == 111)    // 1_L
                        {
                            ResponseClass = new RFID_responseClass() { cmd = "getID", param = rcvParam1 + rcvParam2, value = strRFID_ID };
                            strTemp = JsonConvert.SerializeObject(ResponseClass);
                            OutputArray = System.Text.Encoding.Default.GetBytes(strTemp);
                            tempSocket.Send(OutputArray, strTemp.Length, SocketFlags.None);
                        }
                        else if (FlagCommSend == 112)    // 1_R
                        {
                            ResponseClass = new RFID_responseClass() { cmd = "getID", param = rcvParam1 + rcvParam2, value = strRFID_ID };
                            strTemp = JsonConvert.SerializeObject(ResponseClass);
                            OutputArray = System.Text.Encoding.Default.GetBytes(strTemp);
                            tempSocket.Send(OutputArray, strTemp.Length, SocketFlags.None);
                        }
                        else if (FlagCommSend == 211)    // 2_L
                        {
                            ResponseClass = new RFID_responseClass() { cmd = "getID", param = rcvParam1 + rcvParam2, value = strRFID_ID };
                            strTemp = JsonConvert.SerializeObject(ResponseClass);
                            OutputArray = System.Text.Encoding.Default.GetBytes(strTemp);
                            tempSocket.Send(OutputArray, strTemp.Length, SocketFlags.None);
                        }
                        else if (FlagCommSend == 212)    // 2_R
                        {
                            ResponseClass = new RFID_responseClass() { cmd = "getID", param = rcvParam1 + rcvParam2, value = strRFID_ID };
                            strTemp = JsonConvert.SerializeObject(ResponseClass);
                            OutputArray = System.Text.Encoding.Default.GetBytes(strTemp);
                            tempSocket.Send(OutputArray, strTemp.Length, SocketFlags.None);
                        }
                        else   // no id
                        {
                            ResponseClass = new RFID_responseClass() { cmd = "getID", param = rcvParam1 + rcvParam2, value = "----------" };
                            strTemp = JsonConvert.SerializeObject(ResponseClass);
                            OutputArray = System.Text.Encoding.Default.GetBytes(strTemp);
                            tempSocket.Send(OutputArray, strTemp.Length, SocketFlags.None);
                        }
                        FlagCommSend = 0;// reset
                    }
                    //MessageBox.Show(str);
                }

                catch (System.Exception ex)
                {
                    ex.ToString();
                }

                //Thread.Sleep(500);
            }
        }

        public void WriteLog(string msg)
        {
            string filePath = AppDomain.CurrentDomain.BaseDirectory + "Log";
            if (!Directory.Exists(filePath))
            {
                Directory.CreateDirectory(filePath);
            }
            string logPath = AppDomain.CurrentDomain.BaseDirectory + "Log\\" + DateTime.Now.ToString("yyyy-MM-dd") + ".txt";
            try
            {
                using (StreamWriter sw = File.AppendText(logPath))
                {
                    //sw.WriteLine("消息：" + msg);
                    sw.WriteLine("时间:" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + ": "+msg);
                    //sw.WriteLine("**************************************************");
                    //sw.WriteLine();
                    sw.Flush();
                    sw.Close();
                    sw.Dispose();
                }
            }
            catch (IOException e)
            {
                using (StreamWriter sw = File.AppendText(logPath))
                {
                    sw.WriteLine("异常：" + e.Message);
                    sw.WriteLine("时间：" + DateTime.Now.ToString("yyy-MM-dd HH:mm:ss"));
                    sw.WriteLine("**************************************************");
                    sw.WriteLine();
                    sw.Flush();
                    sw.Close();
                    sw.Dispose();
                }
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            textBox1.Text = " ";
        } 
    }

    public class RFID_cmdClass
    {
        public string cmd { get; set; }
        public string param { get; set; }
    }
    public class RFID_responseClass
    {
        public string cmd { get; set; }
        public string param { get; set; }
        public string value { get; set; }
    }
}
