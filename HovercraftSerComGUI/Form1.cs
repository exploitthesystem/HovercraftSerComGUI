using System;
using System.Collections;
using System.Data;
using System.IO.Ports;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HovercraftSerComGUI
{
    public partial class Form1 : Form
    {
        private SerialPort serialPort;

        public Form1()
        {
            InitializeComponent();
            comBox.Click += new EventHandler(COMbox_Clicked);
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
            string strData, opStr;
            bool isPortValid;
            UInt32 op;

            isPortValid = InitializePort();

            if (isPortValid)
            {
                if (!UInt32.TryParse(opTextBox.Text, System.Globalization.NumberStyles.HexNumber, null, out op))
                    return;

                opStr = opTextBox.Text;

                if (rwCheckBox.Checked)
                {
                    op++;
                    opStr = op.ToString("X");
                }

                opTextBox.Enabled = false;
                rwCheckBox.Enabled = false;
                lengthTextBox.Enabled = false;
                dataTextBox.Enabled = false;

                if (!serialPort.IsOpen)
                    serialPort.Open();

                strData = opStr + lengthTextBox.Text + regStartTextBox.Text + dataTextBox.Text;

                TransmitFrame(strData);

                if (serialPort.IsOpen)
                    serialPort.Close();

                opTextBox.Enabled = true;
                rwCheckBox.Enabled = true;
                lengthTextBox.Enabled = true;
                dataTextBox.Enabled = true;
            }
        }

        // Populates the combo text box with available
        // COM port names.
        private void COMbox_Clicked(object sender, EventArgs e)
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
        private void TransmitFrame(string strData)
        {
            byte[] data;

            if (strData.Length % 2 != 0)
                strData = strData + "0";

            // Add sync header and footer
            strData = "AA55" + strData + "55AA";

            // Convert string message into data byte array
            data = HexStrToByteArray(strData);

            // Set the read/write bit in the OP field
            /*if (rwCheckBox.Checked)
                data[2] = (byte)(data[2] | 0x01);
            else
                data[2] = (byte)(data[2] & 0xFE);*/

            serialPort.Write(data, 0, data.Length);
            sentLabel.Text = "0x" + strData;
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

                if (next_byte.ToString("X") == "AA")
                    read_aa = true;

                if (next_byte.ToString("X") == "55" && read_aa)
                {
                    header_received = true;
                    read_aa = false;
                }

                if (header_received)
                {
                    if (next_byte.ToString("X") == "55")
                        read_55 = true;

                    if (next_byte.ToString("X") == "AA" && read_55)
                        read_byte = false;
                }

                data.Add(next_byte.ToString("X"));
            }

            this.Invoke(new MethodInvoker(() => rcvdLabel.Text = data.ToString()));
        }

        private async void RunMotors(object sender)
        {
            string strData, op, speed;
            bool isPortValid;
            ushort value;

            if (syncMotorBox.Checked)
            {
                op = "22"; // sync motors op
                if (sender == leftMotorBar)
                {
                    value = (ushort)leftMotorBar.Value;
                    rightMotorBar.Value = leftMotorBar.Value;
                }
                else
                {
                    value = (ushort)rightMotorBar.Value;
                    leftMotorBar.Value = rightMotorBar.Value;
                }
            }
            else if (sender == leftMotorBar)
            {
                op = "24"; // left motor op
                value = (ushort)leftMotorBar.Value;
            }
            else
            {
                op = "26"; // right motor op
                value = (ushort)rightMotorBar.Value;
            }

            isPortValid = InitializePort();

            if (isPortValid)
            {
                opTextBox.Enabled = false;
                rwCheckBox.Enabled = false;
                lengthTextBox.Enabled = false;
                dataTextBox.Enabled = false;

                if (!serialPort.IsOpen)
                    serialPort.Open();

                speed = value.ToString("X");

                if (speed.Length == 3)
                {
                    speed = "0" + speed;
                }
                else if (speed.Length == 2)
                {
                    speed = "00" + speed;
                }

                strData = op + "02" + speed;

                TransmitFrame(strData);

                if (serialPort.IsOpen)
                    serialPort.Close();

                await Task.Delay(10);

                opTextBox.Enabled = true;
                rwCheckBox.Enabled = true;
                lengthTextBox.Enabled = true;
                dataTextBox.Enabled = true;
            }
        }

        private byte[] HexStrToByteArray(string hex)
        {
            return Enumerable.Range(0, hex.Length)
                 .Where(x => x % 2 == 0)
                 .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                 .ToArray();
        }
    }
}
