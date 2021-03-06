﻿//
// $Id: MainForm.cs 5084 2013-10-28 23:32:24Z pcbrefugee $
//
//
// Original author: Jay Holman <jay.holman .@. vanderbilt.edu>
//
// Copyright 2011 Vanderbilt University - Nashville, TN 37232
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using CustomDataSourceDialog;
using pwiz.CLI.msdata;

namespace MSConvertGUI
{
    public partial class MainForm : Form
    {
        IList<string> cmdline_args;
        string SetDefaultsDataType = ""; // watch last-added filetype, offer to set defaults for that type
        string AboutButtonHelpText = "Each control has a \"tooltip\": let your cursor rest atop a control for a moment to see a help message for that control.\r\n\r\n" +
            "For more in depth help, visit http://proteowizard.sourceforge.net/tools.shtml .  " +
            "The documentation there describing the command line version of msconvert will be useful for understanding this " +
            "program, especially filters.  Actually, you will generally find the command line version to be more complete and " +
            "flexible than this GUI version and may wish to treat this program as a learning tool for using the command line " +
            "version.\r\n\r\nHere are the various tooltips in a more long lasting form:";
        System.Collections.SortedList sortedToolTips = new System.Collections.SortedList();

        private IList<KeyValuePair<string, string>> thresholdTypes = new KeyValuePair<string, string>[]
        {
            new KeyValuePair<string, string>("Count", "count"),
            new KeyValuePair<string, string>("Count after ties", "count-after-ties"),
            new KeyValuePair<string, string>("Absolute intensity", "absolute"),
            new KeyValuePair<string, string>("Relative to BPI", "bpi-relative"),
            new KeyValuePair<string, string>("Relative to TIC", "tic-relative"),
            new KeyValuePair<string, string>("Fraction of TIC", "tic-cutoff")
        };

        private string MakeConfigfileName()
        {
            string ext = ".";
            if (SetDefaultsDataType != "") // any current input type?
                ext += SetDefaultsDataType + ".";
            // note not calling these files "*.cfg" in order to distinguish them from the boost program_options files
            return Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\MSConvertGUI" + ext + "cmdline";
        }

        public MainForm(IList<string> args)
        {
            cmdline_args = args;
            InitializeComponent();

            Text = "MSConvert" + (Environment.Is64BitProcess ? " (64-bit)" : "");
        }

        private void setFileBoxText(string text)
        {
            if (""!=text)
                lastFileboxText = text; // for use in setting browse directory

            if (text.Count(o => "?*".Contains(o)) > 0)
            {
                string directory = Path.GetDirectoryName(text);
                if (String.IsNullOrEmpty(directory))
                    directory = ".";
                foreach (string filepath in Directory.GetFiles(directory, Path.GetFileName(text)))
                {
                    FileBox.Text = filepath;
                    AddFileButton_Click(this, EventArgs.Empty);
                }
            }
            else
                FileBox.Text = text;
        }

        private void MainForm_Load (object sender, EventArgs e)
        {
            assignTooltips();

            foreach(DataGridViewColumn column in FilterDGV.Columns)
                column.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;

            for (int i = 0; i < cmdline_args.Count; i++)
            {
                // mimic user adding file via filebox
                setFileBoxText(cmdline_args[i]);
                AddFileButton_Click(sender, e);
                if (FileBox.Text.Length > 0)
                {
                    // if it didn't get added, field will still contain file or dir name
                    MessageBox.Show("Don't know how to read \"" + FileBox.Text + "\", ignored",this.Text);
                    setFileBoxText("");
                }
            }
            OutputFormatBox.Text = "mzML";
            FilterBox.Text = "MS Level";
            ActivationTypeBox.Text = "CID";
            // check for a user default config
            String configname = MakeConfigfileName();
            if (File.Exists(configname))
                SetGUIfromCfg(configname); // populate buttons etc from config file

            thresholdTypeComboBox.Items.AddRange(thresholdTypes.Select(o => o.Key).ToArray());
            thresholdTypeComboBox.SelectedIndex = 0;
            thresholdOrientationComboBox.SelectedIndex = 0;
            thresholdValueTextBox.Text = "100";

            ValidateNumpress(); // make sure numpress settings are reasonable

        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // don't let running jobs prevent the process from exiting
            Process.GetCurrentProcess().Kill();
        }

        private void FilterBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            MSLevelPanel.Visible = false;
            PeakPickingPanel.Visible = false;
            ZeroSamplesPanel.Visible = false;
            ETDFilterPanel.Visible = false;
            ThresholdFilterPanel.Visible = false;
            ChargeStatePredictorPanel.Visible = false;
            ActivationPanel.Visible = false;
            SubsetPanel.Visible = false;

            switch (FilterBox.Text)
            {
                case "MS Level":
                    MSLevelPanel.Visible = true;
                    break;
                case "Peak Picking":
                    PeakPickingPanel.Visible = true;
                    break;
                case "Zero Samples":
                    ZeroSamplesPanel.Visible = true;
                    break;
                case "ETD Peak Filter":
                    ETDFilterPanel.Visible = true;
                    break;
                case "Threshold Peak Filter":
                    ThresholdFilterPanel.Visible = true;
                    break;
                case "Charge State Predictor":
                    ChargeStatePredictorPanel.Visible = true;
                    break;
                case "Activation":
                    ActivationPanel.Visible = true;
                    break;
                case "Subset":
                    SubsetPanel.Visible = true;
                    break;
            }
        }

        private bool IsValidSource(string filepath)
        {
            return (File.Exists(filepath) || Directory.Exists(filepath)) &&
                   !FileListBox.Items.Contains(filepath) &&
                   !String.IsNullOrEmpty(ReaderList.FullReaderList.identify(filepath));
        }

        private void FileBox_TextChanged (object sender, EventArgs e)
        {
            AddFileButton.Enabled = IsValidSource(FileBox.Text);
        }

        private void AddFileButton_Click(object sender, EventArgs e)
        {
            if (IsValidSource(FileBox.Text))
            {
                if (String.IsNullOrEmpty(OutputBox.Text))
                    OutputBox.Text = Path.GetDirectoryName(FileBox.Text);
                // update the set-defaults button
                SetDefaultsDataType = ReaderList.FullReaderList.identify(FileBox.Text);
                SetDefaultsButton.Text = "Use these settings next time I start MSConvertGUI with " + SetDefaultsDataType + " data";
                setToolTip(SetDefaultsButton, "Saves the current settings and uses them as the defaults next time you open " + SetDefaultsDataType + " data with MSConvertGUI.");
                // and add to the list
                FileListBox.Items.Add(FileBox.Text);
                FileBox.Clear();
                RemoveFileButton.Enabled = true;
            }
        }

        private void RemoveFileButton_Click(object sender, EventArgs e)
        {
            if (FileListBox.SelectedItems.Count == 1)
            {
                setFileBoxText((string)FileListBox.Items[FileListBox.SelectedIndex]);
                FileListBox.Items.RemoveAt(FileListBox.SelectedIndex);
            }
            else if (FileListBox.SelectedItems.Count > 1)
            {
                setFileBoxText((string)FileListBox.SelectedItems[0]);
                var itemsToDelete = FileListBox.SelectedItems.Cast<object>().ToList();
                foreach (var item in itemsToDelete)
                    FileListBox.Items.Remove(item);
            }
            else
                return;

            RemoveFileButton.Enabled = FileListBox.Items.Count > 0;
            AddFileButton.Enabled = IsValidSource(FileBox.Text);
        }

        private void BrowseFileButton_Click(object sender, EventArgs e)
        {
            var fileList = new List<string>
                               {
                                   "Any spectra format","mzML", "mzXML", "MZ5",
                                   "MS1", "CMS1", "BMS1", "MS2", "CMS2", "BMS2",
                                   "Thermo RAW", "Waters RAW", "ABSciex WIFF",
                                   "Bruker Analysis", "Agilent MassHunter",
                                   "Mascot Generic", "Bruker Data Exchange"
                               };

            OpenDataSourceDialog browseToFileDialog;
            browseToFileDialog = String.IsNullOrEmpty(FileBox.Text)
                                     ? new OpenDataSourceDialog(fileList, lastFileboxText)
                                     : new OpenDataSourceDialog(fileList, FileBox.Text);

            #region Set up Delegates
            browseToFileDialog.FolderType = x =>
                                                {
                                                    try
                                                    {
                                                        string type = ReaderList.FullReaderList.identify(x);
                                                        if (type == String.Empty)
                                                            return "File Folder";
                                                        return type;
                                                    }
                                                    catch { return String.Empty; }
                                                };
            browseToFileDialog.FileType = x =>
                                              {
                                                  try
                                                  {
                                                      return ReaderList.FullReaderList.identify(x);
                                                  }
                                                  catch { return String.Empty; }
                                              };
            #endregion

            if (browseToFileDialog.ShowDialog() == DialogResult.OK)
            {
                if (browseToFileDialog.DataSources.Count == 1)
                    setFileBoxText(browseToFileDialog.DataSources[0]);
                else if (browseToFileDialog.DataSources.Count > 1)
                {
                    foreach (string dataSource in browseToFileDialog.DataSources)
                        FileListBox.Items.Add(dataSource);

                    if (String.IsNullOrEmpty(OutputBox.Text) ||
                        !Directory.Exists(OutputBox.Text))
                        OutputBox.Text = Path.GetDirectoryName(browseToFileDialog.DataSources[0]);

                    RemoveFileButton.Enabled = FileListBox.Items.Count > 0;
                }
            }
        }

        private void FileListBox_KeyUp (object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete)
            {
                if (FileListBox.SelectedIndices.Count > 0)
                {
                    foreach (int selectedIndex in FileListBox.SelectedIndices.Cast<int>().Reverse())
                        FileListBox.Items.RemoveAt(selectedIndex);
                }
                e.Handled = true;
            }
        }

        private void ZeroSamples_ModeChanged(object sender, EventArgs e)
        {
            ZeroSamplesAddMissingFlankCountBox.Enabled = ZeroSamplesAddMissing.Checked;
        }

        private void NumTextBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (!char.IsDigit(e.KeyChar) && !char.IsControl(e.KeyChar))
            {
                e.Handled = true;
            }
        }

        private void AddFilterButton_Click(object sender, EventArgs e)
        {
            switch (FilterBox.Text)
            {
                case "MS Level":
                    if (!String.IsNullOrEmpty(MSLevelLow.Text) ||
                        !String.IsNullOrEmpty(MSLevelHigh.Text))
                        FilterDGV.Rows.Add(new[]
                                               {
                                                   "msLevel",
                                                   String.Format("{0}-{1}", MSLevelLow.Text, MSLevelHigh.Text)
                                               });
                    break;
                case "Peak Picking":
                    if (!String.IsNullOrEmpty(PeakMSLevelLow.Text) ||
                        !String.IsNullOrEmpty(PeakMSLevelHigh.Text))
                        FilterDGV.Rows.Add(new[]
                                               {
                                                   "peakPicking",
                                                   String.Format("{0} {1}-{2}",
                                                                 PeakPreferVendorBox.Checked.ToString().ToLower(),
                                                                 PeakMSLevelLow.Text, PeakMSLevelHigh.Text)
                                               });
                    break; 
                case "Zero Samples":
                    String args = ZeroSamplesAddMissing.Checked ? "addMissing" : "removeExtra";
                    if ( ZeroSamplesAddMissing.Checked && (!String.IsNullOrEmpty(ZeroSamplesAddMissingFlankCountBox.Text)))
                        args+=String.Format("={0}",ZeroSamplesAddMissingFlankCountBox.Text);
                    if (!String.IsNullOrEmpty(ZeroSamplesMSLevelLow.Text) ||
                        !String.IsNullOrEmpty(ZeroSamplesMSLevelHigh.Text))
                        args += String.Format(" {0}-{1}",ZeroSamplesMSLevelLow.Text,ZeroSamplesMSLevelHigh.Text);
                    else // no mslevels specified means all mslevels
                        args += " 1-";
                    FilterDGV.Rows.Add(new[]
                                           {
                                               "zeroSamples",
                                                args
                                           });
                    break; 
                case "ETD Peak Filter":
                    var tempObject = new[] {"ETDFilter", String.Empty};
                    if (!ETDRemovePrecursorBox.Checked || !ETDRemoveChargeReducedBox.Checked ||
                        !ETDRemoveNeutralLossBox.Checked || !ETDBlanketRemovalBox.Checked)
                        tempObject[1] = String.Format("{0} {1} {2} {3}",
                                                      ETDRemovePrecursorBox.Checked.ToString().ToLower(),
                                                      ETDRemoveChargeReducedBox.Checked.ToString().ToLower(),
                                                      ETDRemoveNeutralLossBox.Checked.ToString().ToLower(),
                                                      ETDBlanketRemovalBox.Checked.ToString().ToLower());
                    FilterDGV.Rows.Add(tempObject);
                    break;
                case "Charge State Predictor":
                    if (!String.IsNullOrEmpty(ChaMCMinBox.Text) &&
                        !String.IsNullOrEmpty(ChaMCMaxBox.Text))
                        FilterDGV.Rows.Add(new[]
                                               {
                                                   "chargeStatePredictor",
                                                   String.Format("{0} {1} {2} {3}",
                                                                 ChaOverwriteCharge.Checked.ToString().ToLower(),
                                                                 ChaMCMaxBox.Text, ChaMCMinBox.Text, ChaSingleBox.Value)
                                               });
                    break;
                case "Activation":
                    if (!String.IsNullOrEmpty(ActivationTypeBox.Text))
                        FilterDGV.Rows.Add(new[] {"activation", ActivationTypeBox.Text});
                    break;
                case "Subset":
                    if (!String.IsNullOrEmpty(ScanNumberLow.Text) || !String.IsNullOrEmpty(ScanNumberHigh.Text))
                        FilterDGV.Rows.Add(new[] { "scanNumber", String.Format("{0}-{1}", ScanNumberLow.Text, ScanNumberHigh.Text) });
                    if (!String.IsNullOrEmpty(ScanTimeLow.Text) || !String.IsNullOrEmpty(ScanTimeHigh.Text))
                        FilterDGV.Rows.Add(new[] { "scanTime", String.Format("[{0},{1}]", ScanTimeLow.Text, ScanTimeHigh.Text) });
                    if (!String.IsNullOrEmpty(mzWinLow.Text) || !String.IsNullOrEmpty(mzWinHigh.Text))
                        FilterDGV.Rows.Add(new[] { "mzWindow", String.Format("[{0},{1}]", mzWinLow.Text, mzWinHigh.Text) });
                    break;
                case "Threshold Peak Filter":
                    int thresholdTypeIndex = thresholdTypes.Select(o => o.Key).ToList().IndexOf(thresholdTypeComboBox.SelectedItem.ToString());
                    string thresholdType = thresholdTypes[thresholdTypeIndex].Value; // Count after ties -> count-after-ties
                    string thresholdOrientation = thresholdOrientationComboBox.SelectedItem.ToString().ToLower().Replace(' ', '-'); // Most intense -> most-intense
                    FilterDGV.Rows.Add(new[] { "threshold", String.Format("{0} {1} {2}", thresholdType, thresholdValueTextBox.Text, thresholdOrientation) });
                    break;
            }
        }

        private void RemoveFilterButton_Click(object sender, EventArgs e)
        {
            if (FilterDGV.SelectedRows.Count > 0)
                FilterDGV.Rows.Remove(FilterDGV.SelectedRows[0]);
        }

        private void ValidateNumpress()
        {
            bool numpressEnable = ("mzML" == OutputFormatBox.Text); // meaningless outside of mzML
            NumpressLinearBox.Enabled = numpressEnable;
            NumpressPicBox.Enabled = numpressEnable;
            NumpressSlofBox.Enabled = numpressEnable;
            if (NumpressPicBox.Checked && NumpressSlofBox.Checked)
                NumpressPicBox.Checked = false; // mutually exclusive
        }

        private void OutputFormatBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            ValidateNumpress();
        }

        private void OutputBrowse_Click(object sender, EventArgs e)
        {
            var ofd = new FolderBrowserDialog();
            if (!String.IsNullOrEmpty(OutputBox.Text) && Directory.Exists(OutputBox.Text))
                ofd.SelectedPath = OutputBox.Text;
            if (ofd.ShowDialog() == DialogResult.OK)
                OutputBox.Text = ofd.SelectedPath;
        }

        private void FileListRadio_CheckedChanged(object sender, EventArgs e)
        {
            if (FileListRadio.Checked)
            {
                AddFileButton.Visible = true;
                RemoveFileButton.Visible = true;
                FileListBox.Enabled = true;
            }
            else
            {
                AddFileButton.Visible = false;
                RemoveFileButton.Visible = false;
                FileListBox.Enabled = false;
            }
        }

        private String ConstructCommandline() 
        // if you update this, you probably need to update SetControlsFromCommandline too
        {
            var commandLine = new StringBuilder();
            //Get config settings

            if (OutputFormatBox.Text != OutputExtensionBox.Text)
                commandLine.AppendFormat("--ext|{0}|", OutputExtensionBox.Text);
            ValidateNumpress(); // make sure numpress settings are reasonable
            switch (OutputFormatBox.Text)
            {
                case "mzXML":
                    commandLine.Append("--mzXML|");
                    break;
                case "mz5":
                    commandLine.Append("--mz5|");
                    break;
                case "mgf":
                    commandLine.Append("--mgf|");
                    break;
                case "text":
                    commandLine.Append("--text|");
                    break;
                case "ms1":
                    commandLine.Append("--ms1|");
                    break;
                case "cms1":
                    commandLine.Append("--cms1|");
                    break;
                case "ms2":
                    commandLine.Append("--ms2|");
                    break;
                case "cms2":
                    commandLine.Append("--cms2|");
                    break;
            }

            if (Precision32.Checked)
                commandLine.Append("--32|");

            if (!WriteIndexBox.Checked)
                commandLine.Append("--noindex|");

            if (UseZlibBox.Checked)
                commandLine.Append("--zlib|");

            if (GzipBox.Checked)
                commandLine.Append("--gzip|");

            if (NumpressLinearBox.Checked)
                commandLine.AppendFormat("--numpressLinear|");

            if (NumpressSlofBox.Checked)
                commandLine.AppendFormat("--numpressSlof|");

            if (NumpressPicBox.Checked)
                commandLine.Append("--numpressPic|");

            var msLevelsTotal = String.Empty;
            var peakPickingTotal = String.Empty;
            var scanNumberTotal = String.Empty;
            var preferVendor = true;
            foreach (DataGridViewRow row in FilterDGV.Rows)
            {
                switch ((string)row.Cells[0].Value)
                {
                    case "msLevel":
                        msLevelsTotal += (string)row.Cells[1].Value + " ";
                        break;
                    case "peakPicking":
                        var splitLine = ((string)row.Cells[1].Value ?? "true").Split();
                        preferVendor = bool.Parse(splitLine[0]);
                        if (splitLine.Length > 1)
                            peakPickingTotal += splitLine[1] + " ";
                        break;
                    case "scanNumber":
                        scanNumberTotal += (string)row.Cells[1].Value + " ";
                        break;
                    default:
                        commandLine.AppendFormat("--filter|{0} {1}|", row.Cells[0].Value, row.Cells[1].Value);
                        break;
                }
            }

            if (!String.IsNullOrEmpty(msLevelsTotal))
                commandLine.AppendFormat("--filter|msLevel {0}|", msLevelsTotal.Trim());
            if (!String.IsNullOrEmpty(peakPickingTotal))
                commandLine.AppendFormat("--filter|peakPicking {0} {1}|", preferVendor.ToString().ToLower(),
                                         peakPickingTotal.Trim());
            if (!String.IsNullOrEmpty(scanNumberTotal))
                commandLine.AppendFormat("--filter|scanNumber {0}|", scanNumberTotal.Trim());

            if (MakeTPPCompatibleOutputButton.Checked)
            {
                String tppline = "--filter|titleMaker <RunId>.<ScanNumber>.<ScanNumber>.<ChargeState> File:\"<SourcePath>\", NativeID:\"<Id>\"|";
                if (!commandLine.ToString().Contains(tppline))
                    commandLine.Append(tppline);
            }

            return commandLine.ToString();
        }

        private void SetControlsFromCommandline(string commandLine)
        // if you update this, you probably need to update ConstructCommandLine too
        {
            // Get config settings
            Precision32.Checked = (commandLine.IndexOf("--32")>=0);
            Precision64.Checked = !Precision32.Checked;
            WriteIndexBox.Checked = !(commandLine.IndexOf("--noindex")>=0);
            UseZlibBox.Checked = (commandLine.IndexOf("--zlib")>=0);
            GzipBox.Checked = (commandLine.IndexOf("--gzip")>=0);
            string OutputExtension = "";

            string[] words = commandLine.Split('|');
            for (int i = 0; i < words.Length; i++)
            {
                switch (words[i])
                {
                    case "--ext":
                        OutputExtension = words[++i];
                        break;
                    case "--mzXML":
                    case "--mz5":
                    case "--mgf":
                    case "--text":
                    case "--ms1":
                    case "--cms1":
                    case "--ms2":
                    case "--cms2":
                        OutputFormatBox.Text = words[i].Substring(2);
                        break;
                    case "--filter":
                        var space = words[++i].IndexOf(' ');
                        FilterDGV.Rows.Add(new [] { words[i].Substring(0,space),
                            words[i].Substring(space+1) });
                        break;
                    case "--32":
                    case "--noindex":
                    case "--zlib":
                    case "--gzip":
                        break; // already handled these booleans above
                    case "--numpressLinear":
                        NumpressLinearBox.Checked=true;
                        break;
                    case "--numpressSlof":
                        NumpressSlofBox.Checked=true;
                        break;
                    case "--numpressPic":
                        NumpressPicBox.Checked=true;
                        break;
                    case "":
                        break; // just that trailing "|"
                    default:
                        MessageBox.Show("skipping unknown config item \""+words[i]+"\"");
                        for (int j=i+1;j<words.Length;j++)  // skip any args
                        {
                            if (words[j].StartsWith("--"))
                            {
                                i = j-1;
                                break;
                            }
                            i++;
                        }
                        break;
                }
                if (OutputExtension != "")
                {
                    OutputExtensionBox.Text = OutputExtension;
                }
                ValidateNumpress(); // make sure numpress settings are reasonable
            }
        }


        private void SetDefaultsButton_Click(object sender, EventArgs e)
        {
            setCfgFromGUI();
        }

        private void StartButton_Click(object sender, EventArgs e)
        {
            var filesToProcess = new List<string>();
            var commandLine = ConstructCommandline();

            //Get files or filelist text
            if (FileListRadio.Checked)
            {
                if (FileListBox.Items.Count == 0)
                {
                    MessageBox.Show("No files to process");
                    return;
                }

                filesToProcess.AddRange(from string item in FileListBox.Items select item);
            }
            else if (String.IsNullOrEmpty(FileBox.Text) || !File.Exists(FileBox.Text))
            {
                MessageBox.Show("No files to process");
                return;
            }
            else
            {
                filesToProcess.Add(String.Format("--filelist|\"{0}\"", FileBox.Text));
            }

            string outputFolder = String.IsNullOrEmpty(OutputBox.Text) ? Application.StartupPath
                                                                       : OutputBox.Text;
            if (!Directory.Exists(outputFolder))
            {
                if (MessageBox.Show("The directory \"" + outputFolder + "\" does not exist. Do you want to create it?",
                                    "Create Directory?",
                                    MessageBoxButtons.YesNo,
                                    MessageBoxIcon.Question, MessageBoxDefaultButton.Button1) == DialogResult.No)
                    return;
                Directory.CreateDirectory(outputFolder);
            }


            var pf = new ProgressForm(filesToProcess, outputFolder, commandLine);
            pf.Text = "Conversion Progress";
            pf.ShowDialog();

        }

        private void setCfgFromGUI() // write a config file for current GUI state
        {
            string cfgFileName = MakeConfigfileName();
            if (File.Exists(cfgFileName) &&
                (MessageBox.Show("Config file \"" + cfgFileName + "\" already exists. Do you want to replace it?",
                                    "MSConvertGUI",
                                    MessageBoxButtons.YesNo,
                                    MessageBoxIcon.Question, MessageBoxDefaultButton.Button1) == DialogResult.No))
            {
                return;
            }
            string cmdline = ConstructCommandline();
            File.WriteAllText(cfgFileName, cmdline);
        }

        private void SetGUIfromCfg(string cfgFileName) // populate buttons etc from config file
        {
            if (!File.Exists(cfgFileName))
            {
                MessageBox.Show("Can't find config file \"" + cfgFileName + "\"");
                return;
            }
            string cmdline = File.ReadAllText(cfgFileName);
            SetControlsFromCommandline(cmdline);
        }

        void setToolTip(Control ctl, string text, string filtername="")
        {
            ctlToolTip.UseFading = true;
            ctlToolTip.UseAnimation = true;
            ctlToolTip.IsBalloon = true;

            ctlToolTip.ShowAlways = true;

            ctlToolTip.AutoPopDelay = 5000;
            ctlToolTip.InitialDelay = 1000;
            ctlToolTip.ReshowDelay = 500;

            ctlToolTip.SetToolTip(ctl, text);

            // construct the About box help
            // sometime we use the same tooltip on a label and a control
            // just retain the first use
            for (int i = 0; i < sortedToolTips.Count; i++)
            {
                if (text == sortedToolTips.GetByIndex(i).ToString())
                {
                    return;
                }
            }
            String key="";
            if (filtername.Length > 0)
            {
                if (!filtername.Contains("Filter"))
                    key = "Filters: ";
                key += filtername + ": ";
            }
            else if (ctl.Parent.Text.Length > 0)
                key += ctl.Parent.Text + ": ";
            if ((!(ctl is TextBox)) && (ctl.Text.Length > 0))
                key += ctl.Text;
            else if (! ctl.Name.Contains("Panel"))
                key += ctl.Name;
            if (key.Length > 0)
                sortedToolTips.Add( key, text);
        }

        void assignTooltips()
        {
            setToolTip(this.SetDefaultsButton, "Saves the current settings and uses them as the defaults next time you use MSConvertGUI without a recognized input file type.");
            setToolTip(this.WriteIndexBox, "Include an index in mzML and mzXML output files.");
            setToolTip(this.GzipBox, "This compresses the entire output file using gzip, and adds \".gz\" to the end of the filename.");
            setToolTip(this.UseZlibBox, "Using zlib to compress peak lists results in much smaller mzML and mzXML output files.");
            setToolTip(this.OptionsGB, "Useful options for controlling output format and file size.");
            setToolTip(this.FileListRadio, "Click this for normal operation.");
            setToolTip(this.TextFileRadio, "Click this if your input file actually contains a list of files to be converted.");
            setToolTip(this.AddFilterButton, "Add the filter specifed above to the list below.","Filters");
            setToolTip(this.RemoveFilterButton, "Select a filter in the list below then click here to remove it.", "Filters");
            setToolTip(this.ZeroSamplesMSLevelLow, "Lowest MS level for scans to be treated with this filter.","Zero Samples");
            setToolTip(this.ZeroSamplesMSLevelHigh, "Highest MS level for scans to be treated with this filter (may be left blank).", "Zero Samples");
            setToolTip(this.ZeroSamplesMSLevelLabel, "Perform this filter only on scans with these MS Levels.", "Zero Samples");
            setToolTip(this.ZeroSamplesRemove, "Reduces output file sizes by removing zero values which are not adjacent to nonzero values.", "Zero Samples");
            setToolTip(this.ZeroSamplesPanel, "These filters help with missing or unwanted zero value samples.", "Zero Samples");
            setToolTip(this.PeakMSLevelLow, "Lowest MS level on which to perform peak picking.","Peak Picking");
            setToolTip(this.PeakMSLevelHigh, "Highest MS level on which to perform peak picking (may be left blank).", "Peak Picking");
            setToolTip(this.PeakMSLevelLabel, "Selects the MS levels for scans on which to perform peak picking.", "Peak Picking");
            setToolTip(this.MSLevelLow, "Lowest MS level for scans to include in the conversion.","MS Level");
            setToolTip(this.MSLevelHigh, "Highest MS level to include in the conversion (may be left blank).", "MS Level");
            setToolTip(this.ScanNumberLow, "Lowest scan number to include in the conversion.","Subset");
            setToolTip(this.ScanNumberLabel, "Use this filter to include only scans with a limited range of scan numbers.", "Subset");
            setToolTip(this.mzWinLabel, "Use this filter to include only scans with a limited range of m/z values.", "Subset");
            setToolTip(this.ScanTimeLabel, "Use this filter to include only scans with a limited range of scan times.", "Subset");
            setToolTip(this.mzWinLow, "Lowest m/z value to include in the conversion.", "Subset");
            setToolTip(this.ScanTimeHigh, "Highest scan time to include in the conversion.", "Subset");
            setToolTip(this.mzWinHigh, "Highest m/z value to include in the conversion.", "Subset");
            setToolTip(this.ScanTimeLow, "Lowest scan time to include in the conversion.", "Subset");
            setToolTip(this.ScanNumberHigh, "Highest scan number to include in the conversion (may be left blank).", "Subset");
            setToolTip(this.SubsetPanel, "Set values for one or more subset filters, then click Add.", "Subset");
            setToolTip(this.FilterBox, "This chooses the type of filter that you want to add next.");
            setToolTip(this.AboutButton, "Provides information on using MSConvert.");
            setToolTip(this.FilterGB, "Use these controls to add to the conversion filter list.  The order of the filter list is significant.  In particular, vendor-supplied peakPicking must be first since it only works on raw, untransformed data.");
            setToolTip(this.StartButton, "Click here to begin the conversion process.");
            setToolTip(this.RemoveFileButton, "Select a file to be removed from the conversion list, then click here.");
            setToolTip(this.FileListBox, "Add files to this conversion list by using the Browse button to select a file, then clicking the Add button.");
            setToolTip(this.FileBox, "Use the Browse button or type a filename here, then click Add to add it to the list of files to be converted.");
            setToolTip(this.AddFileButton, "Adds the current file to the conversion list.");
            setToolTip(this.FilterDGV, "Use the controls above to add conversion filters. The order can be significant.");
            setToolTip(this.MakeTPPCompatibleOutputButton, "Check this to use TPP-compatible output settings, e.g. an MGF TITLE format like <basename>.<scan>.<scan>.<charge>.");
            MSDataFile.WriteConfig mwc = new MSDataFile.WriteConfig(); // for obtaining default numpress tolerance
            setToolTip(this.NumpressLinearBox, String.Format("Check this to use numpress linear prediction lossy compression for binary mz and rt data in mzML output (relative accuracy loss will not exceed {0}).  Note that not all mzML readers recognize this format.", mwc.numpressLinearErrorTolerance));
            setToolTip(this.NumpressSlofBox, String.Format("Check this to use numpress short logged float lossy compression for binary intensities in mzML output (relative accuracy loss will not exceed  {0}).  Note that not all mzML readers recognize this format.", mwc.numpressSlofErrorTolerance));
            setToolTip(this.NumpressPicBox, "Check this to use numpress positive integer lossy compression for binary intensities in mzML output (absolute accuracy loss will not exceed 0.5).  Note that not all mzML readers recognize this format.");
            setToolTip(this.ETDFilterPanel, "Use these filter options to remove unreacted and charge-reduced precursor peaks in ETD spectra.","ETD Peak");
            setToolTip(this.ETDRemovePrecursorBox, "Check this to remove unreacted precursor peaks from ETD spectra.","ETD Peak");
            setToolTip(this.ETDRemoveChargeReducedBox, "Check this to remove charge-reduced precursor peaks from ETD spectra.","ETD Peak");
            setToolTip(this.ETDRemoveNeutralLossBox, "Check this to remove prominent neutral losses of the +1 charge-reduced precursor from ETD spectra.","ETD Peak");
            setToolTip(this.ETDBlanketRemovalBox, "Check this for an alternative way of neutral loss filtering using a charge-scaled 60 Da exclusion window below the charge-reduced precursors.","ETD Peak");

            setToolTip(this.ThresholdFilterPanel, "Use this filter to remove small noise peaks or undesirable big peaks. Several different thresholding methods are available.","Threshold");

            string thresholdOrientation = "Controls whether the threshold filter keeps the most intense or the least intense peaks.";
            setToolTip(this.thresholdOrientationLabel, thresholdOrientation, "Threshold");
            setToolTip(this.thresholdOrientationComboBox, thresholdOrientation, "Threshold");

            string thresholdTypeHelp = "The filter can use different thresholding schemes:\r\n" +
                                       "Count types: keeps the most/least intense peaks.\r\n" +
                                       "Absolute: keeps peaks with intensities greater than the threshold.\r\n" +
                                       "Relative types: keeps a peak if its the fraction of BPI/TIC is greater than the threshold.\r\n" +
                                       "Fraction of TIC: keeps as many peaks as needed until the threshold of TIC is accounted for.";
            setToolTip(this.thresholdTypeLabel, thresholdTypeHelp, "Threshold");
            setToolTip(this.thresholdTypeComboBox, thresholdTypeHelp, "Threshold");

            string thresholdValueHelp = "The meaning of this threshold value depends on the threshold type:\r\n" +
                                        "Count types: keeps the <value> most/least intense peaks.\r\n" +
                                        "Absolute: keeps peaks with intensities greater/less than <value>.\r\n" +
                                        "Relative types: keeps a peak if its the fraction of BPI/TIC is greater/less than <value>.\r\n" +
                                        "Fraction of TIC: keeps as many peaks as needed until the fraction <value> of the TIC is accounted for.";
            setToolTip(this.thresholdValueLabel, thresholdValueHelp, "Threshold");
            setToolTip(this.thresholdValueTextBox, thresholdValueHelp, "Threshold");

            setToolTip(this.ChargeStatePredictorPanel, "Use this filter to add missing (and optionally overwrite existing) charge state information to MSn spectra.\r\n" +
                                                       "For CID spectra, the charge state is single/multiple based on %TIC below the precursor m/z.\r\n" +
                                                       "For ETD spectra, the charge state is predicted using the published ETDz SVM prediction model.","Charge State Predictor");
            setToolTip(this.ChaOverwriteCharge, "Check this to overwrite spectra's existing charge state(s) with the predicted ones.", "Charge State Predictor");

            string chaSingleHelp = "When the %TIC below the precursor m/z is less than this value, the spectrum is predicted as singly charged.";
            setToolTip(this.ChaSingleLabel, chaSingleHelp, "Charge State Predictor");
            setToolTip(this.ChaSingleBox, chaSingleHelp, "Charge State Predictor");

            string maxChargeHelp = "Maximum multiple charge state to be predicted.";
            setToolTip(this.ChaMCMaxLabel, maxChargeHelp, "Charge State Predictor");
            setToolTip(this.ChaMCMaxBox, maxChargeHelp, "Charge State Predictor");

            string minChargeHelp = "Minimum multiple charge state to be predicted.";
            setToolTip(this.ChaMCMinLabel, minChargeHelp, "Charge State Predictor");
            setToolTip(this.ChaMCMinBox, minChargeHelp, "Charge State Predictor");

            string OutputExtensionHelp = "Sets the filename extension for the output file(s)";
            setToolTip(this.OutputExtensionLabel, OutputExtensionHelp);
            setToolTip(this.OutputExtensionBox, OutputExtensionHelp);

            string precisionHelp = "Sets output precision for writing binary m/z and intensity information. High resolution instruments should use 64-bit m/z values, but otherwise it just creates unnecessarily large output files.";
            setToolTip(this.PrecisionLabel, precisionHelp);
            setToolTip(this.Precision64, precisionHelp);
            setToolTip(this.Precision32, precisionHelp);

            string activationTypeHelp = "Include only scans with this precursor activation type.";
            setToolTip(this.ActivationTypeLabel, activationTypeHelp, "Activation");
            setToolTip(this.ActivationTypeBox, activationTypeHelp, "Activation");

            string msLevelHelp = "Use this filter to include only scans with certain MS levels.";
            setToolTip(this.MSLevelLabel, msLevelHelp,"MS Level");
            setToolTip(this.MSLevelPanel, msLevelHelp, "MS Level");

            string preferVendorHelp = "Uncheck this box if you prefer ProteoWizard's peak picking algorithm to that provided by the vendor (normally the vendor code works better). Not all input formats have vendor peakpicking, but it's OK to leave this checked.";
            setToolTip(this.PeakPreferVendorBox, preferVendorHelp,"Peak Picking");
            setToolTip(this.PeakPickingPanel, "Use this filter to perform peak picking (centroiding) on the input data.", "Peak Picking");

            string outputHelp = "Choose the directory for writing the converted file(s).";
            setToolTip(this.OutputLabel, outputHelp);
            setToolTip(this.OutputBox, outputHelp);

            string outputFormatHelp = "Selects the output format for the conversion";
            setToolTip(this.FormatLabel, outputFormatHelp);
            setToolTip(this.OutputFormatBox, outputFormatHelp);

            string addZerosHelp = "Adds flanking zero values next to nonzero values where needed, to help with things like smoothing.";
            setToolTip(this.ZeroSamplesAddMissing, addZerosHelp, "Zero Samples");
            setToolTip(this.ZeroSamplesAddMissingFlankCountBox, addZerosHelp, "Zero Samples");
        }

        private void AboutButtonClick(object sender, EventArgs e)
        {
            // Create a scrollable form.
            Form aboutBox = new Form();
            int w = 700, h = 400;
            aboutBox.Size = new System.Drawing.Size(w, h);
            w = aboutBox.ClientRectangle.Width;
            h = aboutBox.ClientRectangle.Height;
            // Create the accept button.
            Button buttonOK = new Button();
            // Create the scrollable help text area.
            TextBox helptext = new TextBox();
            // Set the Multiline property to true.
            helptext.Multiline = true;
            // Add vertical scroll bars to the TextBox control.
            helptext.ScrollBars = ScrollBars.Vertical;
            // Allow the Return key to be entered in the TextBox control.
            helptext.AcceptsReturn = true;
            // Allow the TAB key to be entered in the TextBox control.
            helptext.AcceptsTab = true;
            // Set WordWrap to true to allow text to wrap to the next line.
            helptext.WordWrap = true;

            helptext.Size = new System.Drawing.Size(w, h-(20+buttonOK.Height));
            helptext.Text = this.AboutButtonHelpText+"\r\n";
            for (int i = 0; i < this.sortedToolTips.Count; i++)
            {
                helptext.Text += ("\r\n" + sortedToolTips.GetKey(i) + ":\r\n" + sortedToolTips.GetByIndex(i)+"\r\n");
            }
            buttonOK.Text = "OK";
            // Set the position of the button on the form.
            buttonOK.Location = new Point((w-buttonOK.Width)/2, h-(10+buttonOK.Height));
            // Make button1's dialog result OK.
            buttonOK.DialogResult = DialogResult.OK;

            // Add OK button to the form.
            aboutBox.Controls.Add(buttonOK);
            // Add helptext to the form.
            aboutBox.Controls.Add(helptext);

            
            // Set the caption bar text of the form.   
            aboutBox.Text = this.AboutButton.Text;

            // Define the border style of the form to a dialog box.
            aboutBox.FormBorderStyle = FormBorderStyle.FixedDialog;
            // Set the accept button of the form to button1.
            aboutBox.AcceptButton = buttonOK;
            // Set the start position of the form to the center of the screen.
            aboutBox.StartPosition = FormStartPosition.CenterScreen;


            // Display the form as a modal dialog box.
            aboutBox.ShowDialog();

            aboutBox.Dispose();
        }

        private void NumpressPicBox_CheckedChanged(object sender, EventArgs e)
        {  // numpress Pic and numpress Slof are mutually exclusive
            if (NumpressPicBox.Checked && NumpressSlofBox.Checked)
                NumpressSlofBox.Checked = false;
        }

        private void NumpressSlofBox_CheckedChanged(object sender, EventArgs e)
        {  // numpress Pic and numpress Slof are mutually exclusive
            if (NumpressPicBox.Checked && NumpressSlofBox.Checked)
                NumpressPicBox.Checked = false;
        }
    }
}
