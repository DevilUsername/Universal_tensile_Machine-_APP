﻿// --- WinForms C# logic for tensile test with stretch-by-1mm steps ---
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
using System.Globalization;

namespace Tensile_control
{
    public partial class Form1 : Form
    {
        private Timer weightTimer;
        private Timer ruptureTestTimer;
        private Timer fixedLoadTimer;

        private double maxWeight = 0;
        private bool isRuptureTestRunning = false;
        private double targetWeight = 0;
        private bool isMovementLocked = false;

        public Form1()
        {
            InitializeComponent();
            InitSerial();
            InitWeightTimer();
        }

        void InitSerial()
        {
            try
            {
                serialPort1.PortName = "COM7";
                serialPort1.BaudRate = 9600;
                serialPort1.DataReceived += SerialPort1_DataRecived;
                serialPort1.Open();
                textBoxLog.AppendText("Порт успішно відкрито\n");
            }
            catch (Exception ex)
            {
                MessageBox.Show("EROR 404: " + ex.Message);
            }
        }

        private void InitWeightTimer()
        {
            weightTimer = new Timer();
            weightTimer.Interval = 1000;
            weightTimer.Tick += WeightTimer_Tick;
            weightTimer.Start();
        }

        private void WeightTimer_Tick(object sender, EventArgs e)
        {
            if (serialPort1.IsOpen)
                serialPort1.WriteLine("WEIGHT");
        }

        private void SerialPort1_DataRecived(object sender, SerialDataReceivedEventArgs e)
        {
            string line = serialPort1.ReadLine();

            Invoke((MethodInvoker)(() =>
            {
                if (line.StartsWith("W:"))
                {
                    string weightStr = line.Substring(2).Replace("kg", "").Trim();

                    if (double.TryParse(weightStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double currentWeight))
                    {
                        textBoxLive.Text = currentWeight.ToString("F2") + " кг";

                        if (fixedLoadTimer != null && fixedLoadTimer.Enabled)
                        {
                            if (currentWeight >= targetWeight)
                            {
                                fixedLoadTimer.Stop();
                                textBoxLog.AppendText("Нагрузку досягнуто\n");
                            }
                            else
                            {
                                serialPort1.WriteLine("RIGHT:1");
                            }
                        }

                        if (isRuptureTestRunning && ruptureTestTimer != null && ruptureTestTimer.Enabled)
                        {
                            if (currentWeight > maxWeight)
                                maxWeight = currentWeight;

                            if (maxWeight > 0 && currentWeight < maxWeight * 0.7)
                            {
                                ruptureTestTimer.Stop();
                                isRuptureTestRunning = false;
                                isMovementLocked = true;
                                textBoxLog.AppendText("Розрив зафіксовано!\n");
                                textBoxLog.AppendText($"Максимальна сила: {maxWeight:F2} кг\n");
                                ResetAfterTest();
                            }
                            else
                            {
                                serialPort1.WriteLine("RIGHT:1");
                            }
                        }
                    }
                    else
                    {
                        textBoxLog.AppendText("⚠️ Не вдалося розпізнати вагу: " + weightStr + Environment.NewLine);
                    }
                }
                else
                {
                    textBoxLog.AppendText(line + Environment.NewLine);
                }
            }));
        }

        private bool TryGetDistance(out double distance)
        {
            distance = 0;
            return double.TryParse(textBoxDistance.Text, out distance) && distance > 0;
        }

        private void buttonLeft_Click(object sender, EventArgs e)
        {
            if (isMovementLocked) return;

            if (TryGetDistance(out double distance))
                serialPort1.WriteLine($"LEFT:{distance}");
        }

        private void buttonRight_Click(object sender, EventArgs e)
        {
            if (isMovementLocked) return;

            if (TryGetDistance(out double distance))
                serialPort1.WriteLine($"RIGHT:{distance}");
        }

        private void buttonFixedLoad_Click(object sender, EventArgs e)
        {
            if (isMovementLocked) return;

            if (double.TryParse(textBoxFixedLoad.Text, out targetWeight) && targetWeight > 0)
            {
                if (!serialPort1.IsOpen) return;

                maxWeight = 0;
                serialPort1.WriteLine("RIGHT:1");
                StartFixedLoadTimer();
                textBoxLog.AppendText($"Ціль: {targetWeight} кг\n");
            }
            else
            {
                MessageBox.Show("Введіть коректну цільову вагу");
            }
        }

        private void StartFixedLoadTimer()
        {
            if (fixedLoadTimer == null)
            {
                fixedLoadTimer = new Timer();
                fixedLoadTimer.Interval = 300;
                fixedLoadTimer.Tick += FixedLoadTimer_Tick;
            }
            fixedLoadTimer.Start();
        }

        private void FixedLoadTimer_Tick(object sender, EventArgs e)
        {
            if (serialPort1.IsOpen)
                serialPort1.WriteLine("WEIGHT");
        }

        private void buttonStop_Click(object sender, EventArgs e)
        {
            weightTimer?.Stop();
            fixedLoadTimer?.Stop();
            ruptureTestTimer?.Stop();

            isRuptureTestRunning = false;
            isMovementLocked = true;

            if (serialPort1.IsOpen)
                serialPort1.WriteLine("STOP");

            textBoxLog.AppendText("⛔ Зупинено вручну\n");
        }

        private void buttonStretch_Click(object sender, EventArgs e)
        {
            if (!serialPort1.IsOpen || isMovementLocked) return;

            textBoxLog.AppendText("🔧 Розтягнення зразка...\n");
            maxWeight = 0;
            isRuptureTestRunning = true;
            isMovementLocked = false;

            serialPort1.WriteLine("RIGHT:1");

            if (ruptureTestTimer == null)
            {
                ruptureTestTimer = new Timer();
                ruptureTestTimer.Interval = 300;
                ruptureTestTimer.Tick += RuptureTestTimer_Tick;
            }

            ruptureTestTimer.Start();
        }

        private void RuptureTestTimer_Tick(object sender, EventArgs e)
        {
            if (serialPort1.IsOpen)
                serialPort1.WriteLine("WEIGHT");
        }

        private void ResetAfterTest()
        {
            isMovementLocked = false;
            isRuptureTestRunning = false;

            ruptureTestTimer?.Stop();
            fixedLoadTimer?.Stop();

            textBoxLog.AppendText("✅ Готово до нового тесту\n");
        }

        private void textBoxLive_TextChanged(object sender, EventArgs e) { }
        private void textBox4_TextChanged(object sender, EventArgs e) { }
        private void label1_Click(object sender, EventArgs e) { }
        private void label2_Click(object sender, EventArgs e) { }
        private void label3_Click(object sender, EventArgs e) { }
        private void label6_Click(object sender, EventArgs e) { }
        private void label7_Click(object sender, EventArgs e) { }
        private void label8_Click(object sender, EventArgs e) { }
        private void textBoxLog_TextChanged(object sender, EventArgs e) { }
    }
}
