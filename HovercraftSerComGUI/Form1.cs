using System;
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
        private Semaphore sem = new Semaphore(0, 1);
        private bool connected = false;

        public Form1()
        {
            InitializeComponent();
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
            byte[] data;

            if (connected)
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

        private void SenseButton_Click(object sender, EventArgs e)
        {
            if (connected)
            {
                byte[] data = new byte[8];

                data[0] = 0xAA; // start sync
                data[1] = 0x55;
                data[2] = 0x2D; // opcode I2C
                data[3] = 0x02; // bytes to read
                data[4] = 0x90; // slave address
                data[5] = 0x00; // register
                data[6] = 0x55; // end sync
                data[7] = 0xAA;

                TransmitFrame(data);

                TempLabel.Text = "DEFAULT";
            }
        }

        // Toggle lift fan on or off.
        private void FanButton_Click(object sender, EventArgs e)
        {
            byte[] data = new byte[6];

            if (connected)
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

        /// <summary>
        /// COM drop box event handler. Populates the box with available
        /// COM port names when clicked.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void COMbox_Click(object sender, EventArgs e)
        {
            string[] ports = SerialPort.GetPortNames();

            comBox.Items.Clear();
            comBox.Items.AddRange(ports);
        }

        /// <summary>
        /// COM drop box event handler. Initializes COM port when an
        /// item is selected from the list.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void COMbox_SelectIndexChanged(object sender, EventArgs e)
        {
            if (connected && comBox.Text == serialPort.PortName)
                return;

            bool isPortValid = InitializePort();

            if (isPortValid)
            {
                try
                {
                    serialPort.Open();
                    Thread recThread = new Thread(ReceiveFrame);
                    //Thread senseThread = new Thread(ReadSensor);
                    recThread.Start();
                    //senseThread.Start();
                    connected = true;
                    sem.Release(1);
                }
                catch (Exception)
                {
                    rcvdLabel.Text = "ERROR";
                    connected = false;
                    return;
                }
            }
            else
                connected = false;
        }

        /// <summary>
        /// Event handler for the motor bars. If scrolled, sends
        /// the corresponding value as speed to the motors.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Bar_Scroll(object sender, EventArgs e)
        {
            RunMotors(sender);
        }

        /// <summary>
        /// Event handler for the motor synchronization check box.If checked,
        /// ensures that both motors receive the same speed settings.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SyncMotorBox_CheckedChanged(object sender, EventArgs e)
        {
            if (SyncMotorBox.Checked)
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
                BaudRate = 9600,
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
            sem.WaitOne();

            try
            {
                serialPort.Write(data, 0, data.Length);

                sentLabel.Text = "0x" + ByteArrayToHexStr(data);
            }
            catch (Exception)
            {
                rcvdLabel.Text = "ERROR. Could not send.";
            }

            sem.Release();
        }

        /// <summary>
        /// Receives a frame of the form <0xAA55, data, 0x55AA>.
        /// Reads from serial port buffer and checks for start sync (0xAA55)
        /// and end sync (0x55AA) sequences.
        /// </summary>
        uint CLEAR_BUFF_TIMEOUT = 2000000000;

        private void ReceiveFrame()
        {
            byte[] inBuffer = new byte[64];
            uint clearBuffCounter = 0;
            uint bytesInBuffer = 0;

            while (connected)
            {
                rcvdLabel.Invoke((MethodInvoker)delegate { rcvdLabel.Text = serialPort.BytesToRead.ToString(); });

                if (serialPort.BytesToRead > 0)
                {
                    inBuffer[bytesInBuffer] = (byte)serialPort.ReadByte();

                    if (bytesInBuffer > 0 || inBuffer[bytesInBuffer] == 0xaa)
                    {
                        if (bytesInBuffer == 1 && inBuffer[bytesInBuffer] != 0x55)
                            bytesInBuffer = 0;
                        else
                            bytesInBuffer++;

                    }
                  
                    //CHECK
                    if (bytesInBuffer > 5 && CheckValidFrame(inBuffer, bytesInBuffer))
                    {
                        ParseIncomingBuffer(inBuffer);

                        bytesInBuffer = 0;
                    }

                    clearBuffCounter = 0;
                }

                if (clearBuffCounter > CLEAR_BUFF_TIMEOUT)
                {
                    serialPort.DiscardInBuffer();
                    bytesInBuffer = 0;
                    clearBuffCounter = 0;
                }
            }
        }


        /// <summary>
        /// Verifies that the frame captured by ReceiveFrame conforms to
        /// protocol specifications.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        private bool CheckValidFrame(byte[] data, uint length)
        {
            if (data[0] == 0xAA && data[1] == 0x55 && data[length - 2] == 0x55 && data[length - 1] == 0xaa)
                if (data[3] == length - 6)
                    return true;

            return false;
        }

        /// <summary>
        /// Processes received frame. Prints frame contents.
        /// </summary>
        /// <param name="data"></param>
        private void ParseIncomingBuffer(byte[] data)
        {
            // Add more sensor parsing here

            // Parse temperature reading
            if (data[2] == 0x2D)
                ParseTemp(data);

            // Print data contents
            foreach (byte b in data)
            {
                Console.WriteLine("" + b);
            }

            Invoke((MethodInvoker)delegate { rcvdLabel.Text = "0x" + ByteArrayToHexStr(data); });
        }

        /// <summary>
        /// Communicates with MCU to sample data from remote sensor attached to sensor board.
        /// </summary>
        private void ReadSensor()
        {
            while (connected)
            {
                // Prepare data frame
                byte[] data = new byte[8];

                data[0] = 0xAA; // start sync
                data[1] = 0x55;
                data[2] = 0x2D; // opcode I2C
                data[3] = 0x02; // bytes to read
                data[4] = 0x90; // slave address
                data[5] = 0x00; // register
                data[6] = 0x55; // end sync
                data[7] = 0xAA;

                TransmitFrame(data);

                Thread.Sleep(3000);
            }
        }

        private void ParseTemp(byte[] data)
        {
            double d_c = data[4] + 0.0;

            if (data[5] > 0)
                d_c += 0.5;

            double d_f = d_c * 1.8 + 32;

            Invoke((MethodInvoker)delegate { TempLabel.Text = d_c.ToString() + " °C\n" + d_f.ToString() + " °F"; });
        }

        // Send motor speed data for left, right, or both motors.
        private void RunMotors(object sender)
        {
            byte op;
            byte[] speed = new byte[4];
            byte[] data = new byte[8];

            if (connected)
            {
                if (SyncMotorBox.Checked)
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
                    speed = BitConverter.GetBytes(leftMotorBar.Value);
                }
                else
                {
                    op = 0x26; // right motor op
                    speed = BitConverter.GetBytes(rightMotorBar.Value);
                }

                // Data to send for motor speed control
                data[0] = 0xAA;     // start sync 1st byte
                data[1] = 0x55;     // start sync 2nd byte
                data[2] = op;       // motor
                data[3] = 0x02;     // length
                data[4] = speed[1]; // upper speed byte
                data[5] = speed[0]; // lower speed byte
                data[6] = 0x55;     // end sync 1st byte
                data[7] = 0xAA;     // end sync 2nd byte

                TransmitFrame(data);

                Thread.Sleep(5);
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

        private void TempLabel_Click(object sender, EventArgs e)
        {
            Application.DoEvents();
        }
    }
}
