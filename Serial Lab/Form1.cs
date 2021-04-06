/*
    Serial Lab is an open source project 
    Licensed under the GNU GPLv3
    Author : Ahmed El-Sayed
    ahmed.m.elsayed93@gmail.com
 
*/

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO.Ports;
using System.Windows.Forms.DataVisualization.Charting;
using System.Timers;
using System.IO;
using System.Threading;
using System.Collections;

namespace Seriallab
{
    public partial class MainForm : Form
    {
        public string data { get; set; }
        int graph_scaler = 500;
        int send_repeat_counter = 0;
        bool plotter_flag = false;
        System.IO.StreamWriter out_file;
        System.IO.StreamReader in_file;
        bool send_data_flag = false;
        Queue<string> Funct_Queue;
        string[] Funct_arr;
        public MainForm()
        {
            InitializeComponent();
            configrations();
        }

        public void configrations()
        {
            portConfig.Items.AddRange(SerialPort.GetPortNames());
            baudrateConfig.DataSource = new[] { "115200", "19200", "230400", "57600", "38400", "9600", "4800", "2400" };
            parityConfig.DataSource = new[] { "None", "Odd", "Even", "Mark", "Space" };
            databitsConfig.DataSource = new[] { "5", "6", "7", "8" };
            stopbitsConfig.DataSource = new[] { "1", "2", "1.5" };
            flowcontrolConfig.DataSource = new[] { "None", "RTS", "RTS/X", "Xon/Xoff" };
            //portConfig.SelectedIndex = 0;
            baudrateConfig.SelectedIndex = 1;
            parityConfig.SelectedIndex = 0;
            databitsConfig.SelectedIndex = 3;
            stopbitsConfig.SelectedIndex = 0;
            flowcontrolConfig.SelectedIndex = 0;
            openFileDialog1.Filter = "Text|*.txt";

          //  portConfig.SelectedIndex = 1;
            //mySerial.NewLine = "\r\n>";
            mySerial.NewLine = "\u001b[4;1fSL00>\u001b[K";

            mySerial.DataReceived += rx_data_event;
            tx_repeater_delay.Tick += new EventHandler(send_data);
            backgroundWorker1.DoWork += new DoWorkEventHandler(update_rxtextarea_event);
            //tabControl1.Selected += new TabControlEventHandler(tabControl1_Selecting);

            for (int i = 0; i < 5 && i < 5; i++)
                graph.Series[i].Points.Add(0);

        }

        /*connect and disconnect*/
        private void connect_Click(object sender, EventArgs e)
        {
            /*Connect*/
            if (!mySerial.IsOpen)
            {
                if (Serial_port_config())
                {
                    try
                    {
                        mySerial.Open();
                    }
                    catch
                    {
                        alert("Can't open " + mySerial.PortName + " port, it might be used in another program");
                        return;
                    }

                    if (datalogger_checkbox.Checked)
                    {
                        try
                        {
                            out_file = new System.IO.StreamWriter(datalogger_checkbox.Text, datalogger_append_radiobutton.Checked);
                        }
                        catch
                        {
                            alert("Can't open " + datalogger_checkbox.Text + " file, it might be used in another program");
                            return;
                        }
                    }

                    UserControl_state(true);
                }
            }

            /*Disconnect*/
            else if (mySerial.IsOpen)
            {
                try
                {
                    mySerial.DiscardInBuffer();
                    mySerial.DiscardOutBuffer();
                    mySerial.Close();
                    tx_terminal.Clear();
                    rx_textarea.Clear();
                }
                catch {/*ignore*/}

                if (datalogger_checkbox.Checked)
                    try { out_file.Dispose(); }
                    catch {/*ignore*/ }
                if (in_file != null)
                {
                    try { in_file.Dispose(); }
                    catch {/*ignore*/ }
                }
                UserControl_state(false);
            }
        }

        /* RX -----*/

        /* read data from serial */

        private void rx_data_event(object sender, System.IO.Ports.SerialDataReceivedEventArgs e)
        //private void RX_get_data()
        {
            if (mySerial.IsOpen)
            {
                try
                {
                    //data = mySerial.ReadLine();
                    data = mySerial.ReadLine();
                    //data = mySerial.ReadExisting();
                    if (data != null)
                        this.BeginInvoke((Action)(() =>
                        {

                            if (!backgroundWorker1.IsBusy)
                            {
                                //Print value return
                                backgroundWorker1.RunWorkerAsync();
                                if (rx_textarea.Lines.Count() > 5000)
                                    rx_textarea.ResetText();
                                //rx_textarea.AppendText("[RX]> " + data);
                            }

                            //else if (plotter_flag)
                            // {
                            //Print to chart
                            double number;
                            string t1 = data.Replace("\u001b[K", "").Replace("\0", "");
                            string[] variables = t1.Split(new string[] { "\u001b[3;1f" }, StringSplitOptions.RemoveEmptyEntries);
                            for (int i = 0; i < Funct_arr.Length; i++)
                            {
                                if (variables[0].Contains(Funct_arr[i]) && variables.Length > 1)
                                {
                                    string[] return_value = variables[1].Split(new string[] { ":" }, StringSplitOptions.RemoveEmptyEntries);
                                    if (double.TryParse(return_value[return_value.Length - 1].Replace(" dBm", ""), out number)
                                         && !variables[0].Contains("Unknown Command")
                                        )
                                    {
                                        if (graph.Series[i].Points.Count > graph_scaler)
                                            graph.Series[i].Points.RemoveAt(0);
                                        graph.Series[i].Points.Add(number);
                                    }
                                    graph.ResetAutoValues();

                                    if(return_value[0].Contains("FPW"))
                                    {
                                        FPRW_value.Text = number.ToString();
                                    }
                                   else if (return_value[0].Contains("RPW"))
                                    {
                                        RPWR_value.Text = number.ToString();
                                    }
                                }


                            }
                           
                            //}
                        }));
                }
                catch
                {
                    alert("Can't read form  " + mySerial.PortName + " port it might be opennd in another program");
                }
            }
        }

        /* Append text to rx_textarea*/
        // Backgroundworker1.RunworkerAsync
        private void update_rxtextarea_event(object sender, DoWorkEventArgs e)
        {
            this.BeginInvoke((Action)(() =>
            {
                if (rx_textarea.Lines.Count() > 5000)
                    rx_textarea.ResetText();
                //rx_textarea.AppendText("[RX]> " + data);
                string[] t = data.Split(new string[] { "\u001b[3;1f" }, StringSplitOptions.RemoveEmptyEntries);
                try
                {
                    rx_textarea.AppendText(t[1].Replace("\u001b[K", "").Replace("\0", "") + "\n");
                }
                catch
                {
                    rx_textarea.AppendText(data.Replace("\u001b[K", "").Replace("\0", "") + "\n");
                }
            }));
        }

        /* Enable data logger and log file selection */
        private void datalogger_checkbox_CheckedChanged(object sender, EventArgs e)
        {
            if (datalogger_checkbox.Checked)
            {
                if (openFileDialog1.ShowDialog() == DialogResult.OK)
                {
                    datalogger_checkbox.Text = openFileDialog1.FileName;
                    datalogger_append_radiobutton.Enabled = true;
                    datalogger_overwrite_radiobutton.Enabled = true;
                    datalogger_append_radiobutton.Enabled = true;
                    datalogger_overwrite_radiobutton.Enabled = true;
                }
                else
                {
                    datalogger_checkbox.Checked = false;
                    datalogger_append_radiobutton.Enabled = false;
                    datalogger_overwrite_radiobutton.Enabled = false;
                    datalogger_append_radiobutton.Enabled = false;
                    datalogger_overwrite_radiobutton.Enabled = false;
                }
            }
            else
            {
                datalogger_append_radiobutton.Enabled = false;
                datalogger_overwrite_radiobutton.Enabled = false;
                datalogger_checkbox.Text = "Enable Data logger";
            }
        }

        /* clear rx textarea */
        private void clear_rx_textarea_Click(object sender, EventArgs e)
        {
            rx_textarea.Clear();
        }

        /*TX------*/

        /* Write data to serial port */
        //tx_repeater_delay.Tick envent
        private void sendData_Click(object sender, EventArgs e)
        {
            if (!send_data_flag)
            {
                if (tx_textarea != null)
                {
                    Funct_Queue = new Queue<string>(tx_textarea.Text.Split(new string[] { @" " }, StringSplitOptions.RemoveEmptyEntries));
                    Funct_arr = tx_textarea.Text.Split(new string[] { @" " }, StringSplitOptions.RemoveEmptyEntries);
                }
                else
                {
                    Funct_Queue = null;
                }
                tx_repeater_delay.Interval = (int)send_delay.Value;
                tx_repeater_delay.Start();

                if (send_word_radiobutton.Checked)
                {
                    progressBar1.Maximum = Funct_Queue == null ? (int)send_repeat.Value : (int)send_repeat.Value * Funct_Queue.Count;
                    progressBar1.Visible = true;
                    //progressBar1.Value = (int)send_repeat.Value; 
                    progressBar1.Update();
                }
                else if (write_form_file_radiobutton.Checked)
                {
                    try
                    {
                        in_file = new System.IO.StreamReader(tx_textarea.Text, true);
                    }
                    catch
                    {
                        alert("Can't open " + tx_textarea.Text + " file, it might be not exist or it is used in another program");
                        return;
                    }

                    progressBar1.Maximum = file_size(tx_textarea.Text);
                    progressBar1.Visible = true;
                }
                tx_repeater_delay.Interval = (int)send_delay.Value;
                tx_repeater_delay.Start();


                send_data_flag = true;
                tx_num_panel.Enabled = false;
                tx_textarea.Enabled = false;
                tx_radiobuttons_panel.Enabled = false;
                sendData.Text = "Stop";
                //run task
                //Send_Multidata(tx_textarea.Text.Split(new string[] { @"\n" }, StringSplitOptions.RemoveEmptyEntries));
            }
            else
            {
                tx_repeater_delay.Stop();
                progressBar1.Value = 0;
                send_repeat_counter = 0;
                send_data_flag = false;
                progressBar1.Visible = false;
                tx_num_panel.Enabled = true;
                tx_textarea.Enabled = true;
                tx_radiobuttons_panel.Enabled = true;
                sendData.Text = "Send";
                if (write_form_file_radiobutton.Checked)
                    try
                    {
                        in_file.Dispose();

                    }
                    catch { }

            }
        }
        //tx_repeater_delay.Tick envent
        private void send_data(object sender, EventArgs e)
        {

            string tx_data = "";
            if (send_word_radiobutton.Checked)
            {
                tx_data = tx_textarea.Text;

                if (send_repeat_counter < progressBar1.Maximum)
                {
                    send_repeat_counter++;
                    progressBar1.Value = send_repeat_counter;
                    progressBar1.Update();
                }
                else if ((int)send_repeat.Value > 0 && send_repeat_counter >= progressBar1.Maximum)
                    send_data_flag = false;
            }

            else if (write_form_file_radiobutton.Checked)
            {
                try { tx_data = in_file.ReadLine(); }
                catch { }

                if (tx_data == null)
                    send_data_flag = false;
                else
                {
                    progressBar1.Value = send_repeat_counter;
                    send_repeat_counter++;
                }
                tx_data += "\\n";
            }

            if (send_data_flag)
            {
                if (mySerial.IsOpen)
                {
                    try
                    {

                        //string[] funct_arr = tx_data.Split(new string[] { @" " }, StringSplitOptions.RemoveEmptyEntries);
                        string fu = Funct_Queue.Dequeue();
                        char[] fu_CharArr = fu.ToCharArray();
                        Funct_Queue.Enqueue(fu);
                        for(int i = 0; i<= fu_CharArr.Length;i++)
                        {
                            if (i < fu_CharArr.Length)
                            {
                                mySerial.Write(fu_CharArr[i].ToString());
                                Thread.Sleep(100);
                            }
                            else
                            {
                                mySerial.Write("\r\n");
                                Thread.Sleep(100);
                            }
                        }
                        //mySerial.Write("f");
                        //Thread.Sleep(100);
                        //mySerial.Write("p");
                        //Thread.Sleep(100);
                        //mySerial.Write("w");
                        //Thread.Sleep(100);
                        //mySerial.Write("\r\n");
                        // mySerial.Write("\u0066\u0070\u0077\x0D");// + Environment.NewLine);
                        tx_terminal.AppendText("[TX]> " + fu + "\n");

                    }
                    catch
                    {
                        alert("Can't write to " + mySerial.PortName + " port it might be opennd in another program");
                    }
                }
            }
            else
            {
                tx_repeater_delay.Stop();
                sendData.Text = "Send";
                send_repeat_counter = 0;
                progressBar1.Value = 0;
                progressBar1.Visible = false;
                tx_radiobuttons_panel.Enabled = true;
                tx_num_panel.Enabled = true;
                tx_textarea.Enabled = true;

                if (write_form_file_radiobutton.Checked)
                    try { in_file.Dispose(); }
                    catch { }
            }
        }

        /* write data when keydown*/
        private void tx_textarea_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (key_capture_radiobutton.Checked && mySerial.IsOpen)
            {
                try
                {
                    mySerial.Write(e.KeyChar.ToString());
                    tx_terminal.AppendText("[TX]> " + e.KeyChar.ToString() + "\n");
                    tx_textarea.Clear();
                }
                catch { alert("Can't write to " + mySerial.PortName + " port it might be opennd in another program"); }
            }
        }


        private void send_word_radiobutton_CheckedChanged(object sender, EventArgs e)
        {
            tx_textarea.Clear();
            send_repeat.Enabled = send_word_radiobutton.Checked;
            send_delay.Enabled = send_word_radiobutton.Checked;
            this.ActiveControl = tx_textarea;
        }
        private void key_capture_radiobutton_CheckedChanged(object sender, EventArgs e)
        {
            tx_textarea.Clear();
            send_repeat.Enabled = !key_capture_radiobutton.Checked;
            send_delay.Enabled = !key_capture_radiobutton.Checked;
            sendData.Enabled = !key_capture_radiobutton.Checked;
            this.ActiveControl = tx_textarea;
        }
        private void write_form_file_radiobutton_CheckedChanged(object sender, EventArgs e)
        {
            tx_textarea.Clear();
            send_repeat.Enabled = !write_form_file_radiobutton.Checked;
            send_delay.Enabled = write_form_file_radiobutton.Checked;

            if (write_form_file_radiobutton.Checked)
                if (openFileDialog1.ShowDialog() == DialogResult.OK)
                {
                    tx_textarea.Text = openFileDialog1.FileName;
                    tx_textarea.Cursor = Cursors.Hand;
                    tx_textarea.ReadOnly = true;
                }
                else
                {
                    send_word_radiobutton.Checked = true;
                }
            else
            {
                tx_textarea.Cursor = Cursors.IBeam;
                tx_textarea.ReadOnly = false;
            }
        }

        /* Plotter ------*/
        private void graph_speed_ValueChanged(object sender, EventArgs e)
        {
            graph.ChartAreas[0].AxisY.Interval = (int)graph_speed.Value;
        }
        /* change graph scale*/
        private void graph_scale_ValueChanged(object sender, EventArgs e)
        {
            graph_scaler = (int)graph_scale.Value;
            for (int i = 0; i < 5; i++)
                graph.Series[i].Points.Clear();
        }
        /* set graph max value*/
        private void set_graph_max_enable_CheckedChanged(object sender, EventArgs e)
        {
            if (set_graph_max_enable.Checked)
                try
                {

                    if (graph_max.Value > graph_min.Value)
                        graph.ChartAreas[0].AxisY.Maximum = (int)graph_max.Value;
                    else
                        graph_max.Value = (int)graph.ChartAreas[0].AxisY.Maximum;
                }
                catch { alert("Invalid Minimum value"); }
            else
                graph.ChartAreas[0].AxisY.Maximum = Double.NaN;

            graph_max.Enabled = set_graph_max_enable.Checked;
        }
        private void graph_max_ValueChanged(object sender, EventArgs e)
        {
            if (graph_max.Value > graph_min.Value)
                graph.ChartAreas[0].AxisY.Maximum = (int)graph_max.Value;
            else
            {
                graph_max.Value = (int)graph.ChartAreas[0].AxisY.Maximum;
                alert("Invalid Maximum value");
            }
        }
        /* set graph min value*/
        private void set_graph_min_enable_CheckedChanged(object sender, EventArgs e)
        {
            if (set_graph_min_enable.Checked)
                try
                {
                    //graph_min.Value = (int)graph.ChartAreas[0].AxisY.Minimum;
                    if (graph_min.Value < graph_max.Value)
                        graph.ChartAreas[0].AxisY.Minimum = (int)graph_min.Value;
                    else
                        graph_min.Value = (int)graph.ChartAreas[0].AxisY.Minimum;
                }
                catch { alert("Invalid Minimum value"); }
            else

                graph.ChartAreas[0].AxisY.Minimum = Double.NaN;

            graph_min.Enabled = set_graph_min_enable.Checked;
        }
        private void graph_min_ValueChanged(object sender, EventArgs e)
        {
            if (graph_min.Value < graph_max.Value)
                graph.ChartAreas[0].AxisY.Minimum = (int)graph_min.Value;
            else
            {
                graph_min.Value = (int)graph.ChartAreas[0].AxisY.Minimum;
                alert("Invalid Minimum value");
            }
        }
        /* save graph as image*/
        private void saveAsImageToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (saveFileDialog1.ShowDialog() == DialogResult.OK)
                graph.SaveImage(saveFileDialog1.FileName, ChartImageFormat.Png);
        }
        /*clear graph*/
        private void clear_graph_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < 5; i++)
                graph.Series[i].Points.Clear();
        }

        /*Application-----*/
        /*serial port config*/
        private bool Serial_port_config()
        {
            try { mySerial.PortName = portConfig.Text; }
            catch { alert("There are no available ports"); return false; }
            mySerial.BaudRate = (Int32.Parse(baudrateConfig.Text));
            mySerial.StopBits = (StopBits)Enum.Parse(typeof(StopBits), (stopbitsConfig.SelectedIndex + 1).ToString(), true);
            mySerial.Parity = (Parity)Enum.Parse(typeof(Parity), parityConfig.SelectedIndex.ToString(), true);
            mySerial.DataBits = (Int32.Parse(databitsConfig.Text));
            mySerial.Handshake = (Handshake)Enum.Parse(typeof(Handshake), flowcontrolConfig.SelectedIndex.ToString(), true);

            return true;
        }

        private void UserControl_state(bool value)
        {
            serial_options_group.Enabled = !value;
            datalogger_options_panel.Enabled = !value;
            write_options_group.Enabled = value;

            if (value)
            {
                connect.Text = "Disconnected";
                toolStripStatusLabel1.Text = "Connected port: " + mySerial.PortName + " @ " + mySerial.BaudRate + " bps";
            }
            else
            {
                connect.Text = "Connected";
                toolStripStatusLabel1.Text = "No Connection";
            }
        }

        /* tabcontrol*/
        void tabControl1_Selecting(object sender, TabControlEventArgs e)
        {
            if (tabControl1.SelectedIndex == 2)
                plotter_flag = true;
            else
                plotter_flag = false;
        }
        /* Search for available serial ports */
        private void portConfig_Click(object sender, EventArgs e)
        {
            portConfig.Items.Clear();
            portConfig.Items.AddRange(SerialPort.GetPortNames());
        }
        /*alert function*/
        private void alert(string text)
        {
            alert_messege.Icon = Icon;
            alert_messege.Visible = true;
            alert_messege.ShowBalloonTip(5000, "SSPA", text, ToolTipIcon.Error);
        }
        /*about box*/
        private void toolStripStatusLabel2_Click(object sender, EventArgs e)
        {
            AboutBox1 a = new AboutBox1();
            a.ShowDialog();
        }
        /* Close serial port when closing*/
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (mySerial.IsOpen)
                mySerial.Close();
        }
        private void tx_textarea_Click(object sender, EventArgs e)
        {
            if (write_form_file_radiobutton.Checked)
                write_form_file_radiobutton_CheckedChanged(sender, e);
        }
        /*get number of lines*/
        private int file_size(string path)
        {
            var file = new StreamReader(path).ReadToEnd();
            string[] lines = file.Split(new char[] { '\n' });
            int count = lines.Count();
            return count;
        }

        private void toolStripMenuItem1_Click(object sender, EventArgs e)
        {
            tx_terminal.Clear();
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox1.Checked == true)
            {
                send_repeat.Minimum = 0;
                send_repeat.Value = 0;
                send_repeat.Enabled = false;
            }
            if (checkBox1.Checked == false)
            {
                send_repeat.Minimum = 1;
                send_repeat.Value = 1;
                send_repeat.Enabled = true;
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {

        }

        private void label5_Click(object sender, EventArgs e)
        {

        }

        private void label6_Click(object sender, EventArgs e)
        {

        }

        private void label9_Click(object sender, EventArgs e)
        {

        }

        private void label8_Click(object sender, EventArgs e)
        {

        }

        private void MainForm_Load(object sender, EventArgs e)
        {

        }

        private void groupBox2_Enter(object sender, EventArgs e)
        {

        }

        private void groupBox2_Enter_1(object sender, EventArgs e)
        {

        }

        private void label5_Click_1(object sender, EventArgs e)
        {

        }

        private void label9_Click_1(object sender, EventArgs e)
        {

        }
    }
}















