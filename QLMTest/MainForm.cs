using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace QLMTest
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            QLM.LicenseValidator lv = new QLM.LicenseValidator();

            bool needsActivation = false;
            string errorMsg = string.Empty;

            if (lv.ValidateLicenseAtStartup(Environment.MachineName, ref needsActivation, ref errorMsg) == false)
            {
                LicenseActivationFrm licenseFrm = new LicenseActivationFrm();
                licenseFrm.ShowDialog();

                if (lv.ValidateLicenseAtStartup(Environment.MachineName, ref needsActivation, ref errorMsg) == false)
                {
                    Environment.Exit(0);
                }
            }
        }
    }
}
