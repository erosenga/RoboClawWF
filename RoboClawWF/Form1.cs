﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RoboClawWF
{
    public partial class Form1 : Form
    {
        public string CurrentMacro = "RoboClaw.tst.txt";
        public RoboClawController RoboClawController;
        public Form1()
        {

            InitializeComponent();
            button2.Text = CurrentMacro;
            RoboClawController = new RoboClawController(CurrentMacro, this);

        }
        // run macro
        private void button1_Click(object sender, EventArgs e)
        {
            Control[] macro = this.Controls.Find("button2", true);
            string CurrentMacro = macro[0].Text;
            MacroRunner macroRunner = new MacroRunner(RoboClawController, CurrentMacro);
            macroRunner.RunMacro();
        }

        // Select macro
        private void button2_Click(object sender, EventArgs e)
        {
            var picker = new OpenFileDialog();
            picker.FileName = CurrentMacro;
            picker.DefaultExt = "txt";
            picker.InitialDirectory = Environment.CurrentDirectory;
            picker.Filter = "txt files (*.txt)|*.txt";
            if (picker.ShowDialog() == DialogResult.OK)
            {
                CurrentMacro = picker.FileName;
                button2.Text = CurrentMacro;

            }
        }

        public void SetStatus(string s)
        {
            button3.Text = s;
        }

        private void progressBar1_Click(object sender, EventArgs e)
        {

        }

        private void progressBar2_Click(object sender, EventArgs e)
        {

        }
    }
}
