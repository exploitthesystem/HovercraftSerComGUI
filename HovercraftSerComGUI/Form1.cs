using System;
using System.Collections;
using System.Data;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace HovercraftSerComGUI
{
    public partial class Form1 : Form
    {
        private SerialPort serialPort;

        public Form1()
        {
            InitializeComponent();
            comBox.Click += new EventHandler(COMbox_Click);
            leftMotorBar.Scroll += new EventHandler(Bar_Scroll);
            rightMotorBar.Scroll += new EventHandler(Bar_Scroll);

            //Thread newThread = new Thread(ReceiveFrame);
            //newThread.Start();
        }

        /// <summary>
        /// Send a packet across the COM port specified in the COM text box. 
        /// Checks that the port is valid and open.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SendButton_Click(object sender, EventArgs e)
        {
            string strData;
            bool isPortValid;
            byte[] data;

            isPortValid = InitializePort();

            if (isPortValid)
            {
                if (!serialPort.IsOpen)
                    serialPort.Open();

                strData = opTextBox.Text + lengthTextBox.Text + regStartTextBox.Text + dataTextBox.Text;
                strData = "AA55" + strData + "55AA";

                data = HexStrToByteArray(strData);

                if (rwCheckBox.Checked)
                    data[2] = (byte)(data[2] | 0x01);

                TransmitFrame(data);

                if (serialPort.IsOpen)
                    serialPort.Close();

                Thread.Sleep(1);
            }
        }

        // Toggle lift fan on or off.
        private void FanButton_Click(object sender, EventArgs e)
        {
            bool isPortValid;
            byte[] data = new byte[6];

            isPortValid = InitializePort();

            if (isPortValid)
            {
                data[0] = 0xAA;
                data[1] = 0x55;
                data[2] = 0x29;
                data[3] = 0x00;
                data[4] = 0x55;
                data[5] = 0xAA;

                TransmitFrame(data);
                Thread.Sleep(1);
            }
        }

        // Populates the combo text box with available
        // COM port names.
        private void COMbox_Click(object sender, EventArgs e)
        {
            string[] ports = SerialPort.GetPortNames();

            comBox.Items.AddRange(ports);
        }

        private void Bar_Scroll(object sender, EventArgs e)
        {
            RunMotors(sender);
        }

        private void syncMotorBox_CheckedChanged(object sender, EventArgs e)
        {
            if (syncMotorBox.Checked)
                rightMotorBar.Value = leftMotorBar.Value;
        }

        /// <summary>
        /// Initialize COM port to protocol specification.
        /// </summary>
        /// <returns></returns>
        private bool InitializePort()
        {
            bool portValid = false;

            if (comBox.Text == "")
                return portValid;

            serialPort = new SerialPort
            {
                PortName = comBox.Text,
                BaudRate = 19200,
                Parity = Parity.None,
                StopBits = StopBits.One,
                DataBits = 8,
                Handshake = Handshake.None,
                RtsEnable = true,

                ReadTimeout = 500,
                WriteTimeout = 500
            };

            foreach (string s in SerialPort.GetPortNames())
            {
                if (comBox.Text == s)
                    portValid = true;
            }

            return portValid;
        }

        /// <summary>
        /// Transmits a frame of the form <0xAA55, strData, 0x55AA>
        /// </summary>
        /// <param name="strData"></param>
        private void TransmitFrame(byte[] data)
        {
            serialPort.Write(data, 0, data.Length);

            sentLabel.Text = "0x" + ByteArrayToHexStr(data);
        }

        /// <summary>
        /// Receives a frame of the form <0xAA55, data, 0x55AA>
        /// </summary>
        private void ReceiveFrame()
        {
            bool read_byte = true;
            bool read_aa = false;
            bool read_55 = false;
            bool header_received = false;

            int next_byte;
            ArrayList data = new ArrayList();

            while (read_byte)
            {
                next_byte = serialPort.ReadByte();

                if (next_byte == 0xAA)
                    read_aa = true;

                if (next_byte == 0x55 && read_aa)
                {
                    header_received = true;
                    read_aa = false;
                }

                if (header_received)
                {
                    if (next_byte == 0x55)
                        read_55 = true;

                    if (next_byte == 0xAA && read_55)
                        read_byte = false;
                }

                data.Add(next_byte.ToString("X"));
            }

            Invoke(new MethodInvoker(() => rcvdLabel.Text = "0x" + data.ToString()));
        }

        // Send motor speed data for left, right, or both motors.
        private void RunMotors(object sender)
        {
            bool isPortValid;
            byte op;
            byte[] speed = new byte[4];
            byte[] data = new byte[8];

            if (syncMotorBox.Checked)
            {
                op = 0x22; // sync motors op
                if (sender == leftMotorBar)
                {
                    speed = BitConverter.GetBytes(leftMotorBar.Value);
                    rightMotorBar.Value = leftMotorBar.Value;
                }
                else
                {
                    speed = BitConverter.GetBytes(rightMotorBar.Value);
                    leftMotorBar.Value = rightMotorBar.Value;
                }
            }
            else if (sender == leftMotorBar)
            {
                op = 0x24; // left motor op
                rightMotorBar.Value = leftMotorBar.Value;
            }
            else
            {
                op = 0x26; // right motor op
                speed = BitConverter.GetBytes(rightMotorBar.Value);
            }

            isPortValid = InitializePort();

            if (isPortValid)
            {
                data[0] = 0xAA;     // start sync 1st byte
                data[1] = 0x55;     // start sync 2nd byte
                data[2] = op;       // motor
                data[3] = 0x02;     // length
                data[4] = speed[1]; // upper speed byte
                data[5] = speed[0]; // lower speed byte
                data[6] = 0x55;     // end sync 1st byte
                data[7] = 0xAA;     // end sync 2nd byte

                if (!serialPort.IsOpen)
                    serialPort.Open();

                TransmitFrame(data);

                if (serialPort.IsOpen)
                    serialPort.Close();

                Thread.Sleep(1);
            }
        }

        // Converts a hexadecimal string to an array of bytes.
        private byte[] HexStrToByteArray(string hex)
        {
            return Enumerable.Range(0, hex.Length)
                 .Where(x => x % 2 == 0)
                 .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                 .ToArray();
        }

        // Converts an array of bytes to a hexadecimal string.
        private string ByteArrayToHexStr(byte[] ba)
        {
            return BitConverter.ToString(ba).Replace("-", "");
        }
    }
}
