using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace StepperWF
{
    public partial class Form1 : Form
    {
        public string CurrentMacro="stepper.tst.txt";
        public StepperController stepperController;
        public Form1()
        {
            InitializeComponent();
            stepperController = new StepperController();
            button2.Text = CurrentMacro;
        }
        // run macro
        private void button1_Click( object sender, EventArgs e )
        {
            Control[] macro = this.Controls.Find( "button2", true );
            string CurrentMacro = macro[0].Text;
            MacroRunner macroRunner = new MacroRunner(stepperController,  CurrentMacro, stepperController.serialPort );
            macroRunner.RunMacro();
        }

        // Select macro
        private void button2_Click( object sender, EventArgs e )
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

    }
}
