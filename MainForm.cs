/*******************************************************************************
*
* Example program:
*   Rfsa Self Calibration
*
* Category:
*   NI-RFSA
*
* Description:
*   Use this example to learn how to do self calibration for the NI-RFSA.
                         

* Instructions for running:
*   1. Configure both RFSA device in the MAX for the program to run. 
*
*	2. Configure the Clock Source in the UI.
*   
*   3. Configure the Self Calibration Step Operation.
*
*   4. Select the Start Button in the UI to start the self-calibration.
*   
*   5. The data is displayed in the DataGrid.
*
* I/O Connections Overview:
*   Make sure your signal input terminals match the Physical Channel I/O
*   Controls.  If you have a PXI chassis, ensure that it has been properly
*   identified in MAX.  
*
*******************************************************************************/
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using NationalInstruments.ModularInstruments.NIDCPower;
using NationalInstruments.ModularInstruments.NIRfsa;
using NationalInstruments.ModularInstruments.NIRfsg;
using NationalInstruments.ModularInstruments.SystemServices.DeviceServices;
using NationalInstruments.RFmx.InstrMX;
using NationalInstruments.SystemConfiguration;

namespace SelfCalibration
{
    public partial class MainForm : Form
    {
        private NIRfsa rfsaSession;
        private NIRfsg rfsgSession;
        RFmxInstrMX vst;

        public MainForm()
        {
            InitializeComponent();
            LoadRfsaDeviceNames();
            ConfigureOutputTerminalComboBox();
            ConfigureSelfCalibrationStepOperation();
            indicatorLabel.BackColor = Color.Gray;
        }

        #region Initial Configuration

        private void LoadRfsaDeviceNames()
        {
            ModularInstrumentsSystem modularInstrumentsSystem = new ModularInstrumentsSystem("NI-RFSA");

            foreach (DeviceInfo device in modularInstrumentsSystem.DeviceCollection)
                resourceNameComboBox.Items.Add(device.Name);
            if (modularInstrumentsSystem.DeviceCollection.Count > 0)
                resourceNameComboBox.SelectedIndex = 0;


            ModularInstrumentsSystem _dcPowerLists = new ModularInstrumentsSystem("NI-DCPower");
            foreach (DeviceInfo _dc in _dcPowerLists.DeviceCollection)
            {
                lbxDCs.Items.Add(_dc.Name);
            }
        }

        private void ConfigureOutputTerminalComboBox()
        {
            var clockSourceValueList = new List<KeyValuePair<string, RfsaReferenceClockSource>>();
            clockSourceValueList.Add(new KeyValuePair<string, RfsaReferenceClockSource>("OnboardClock", RfsaReferenceClockSource.OnboardClock));
            clockSourceValueList.Add(new KeyValuePair<string, RfsaReferenceClockSource>("RefIn", RfsaReferenceClockSource.ReferenceIn));
            clockSourceValueList.Add(new KeyValuePair<string, RfsaReferenceClockSource>("PXI_Clk", RfsaReferenceClockSource.PxiClock));
            clockSourceValueList.Add(new KeyValuePair<string, RfsaReferenceClockSource>("ClkIn", RfsaReferenceClockSource.ClockIn));
            clockSourceComboBox.DisplayMember = "Key";
            clockSourceComboBox.ValueMember = "Value";
            clockSourceComboBox.DataSource = clockSourceValueList;
            clockSourceComboBox.SelectedIndex = 0;
        }

        private void ConfigureSelfCalibrationStepOperation()
        {
            var selfCalibrationOperationValueList = new List<string>();
            selfCalibrationOperationValueList.Add("NPI Quick Range");
            selfCalibrationOperationValueList.Add("Perform Neccessary Self Calibration");
            selfCalibrationOperationValueList.Add("Perform All Self Calibration Step");
            selfCalibrationOperationValueList.Add("Omit IF Flatness Self Calibration");
            selfCalibrationComboBox.DataSource = selfCalibrationOperationValueList;
        }

        #endregion

        #region UI Gets

        private string ResourceName
        {
            get
            {
                return this.resourceNameComboBox.Text;
            }
        }

        public RfsaReferenceClockSource ReferenceClockSource
        {
            get
            {
                return this.clockSourceComboBox.SelectedValue as RfsaReferenceClockSource ?? RfsaReferenceClockSource.FromString(this.clockSourceComboBox.Text);
            }
        }

        private int SelfCalibrationOperation
        {
            get
            {
                return this.selfCalibrationComboBox.SelectedIndex;
            }
        }

        #endregion

        private void InitializeRfsaSession()
        {
            CloseSession();
            RFMXExtension.ConfigureDebugSettings(ResourceName, false, false);

            rfsgSession = new NIRfsg(ResourceName, false, false, "DriverSetup=Bitfile:NI-RFIC.lvbitx");
            vst = new RFmxInstrMX(ResourceName, "DriverSetup=Bitfile:NI-RFIC.lvbitx");
            vst.DangerousGetNIRfsaHandle(out IntPtr niRfsaHandle);
            rfsaSession = new NIRfsa(niRfsaHandle);

            rfsaSession.DriverOperation.Warning += new System.EventHandler<RfsaWarningEventArgs>(SADriverOperationWarning);
            rfsgSession.DriverOperation.Warning += new System.EventHandler<RfsgWarningEventArgs>(SGDriverOperationWarning);
        }

        private void SADriverOperationWarning(object sender, RfsaWarningEventArgs e)
        {
            MessageBox.Show(e.Warning.ToString(), "Warning");
        }
        private void SGDriverOperationWarning(object sender, RfsgWarningEventArgs e)
        {
            MessageBox.Show(e.Warning.ToString(), "Warning");
        }

        private void CloseSession()
        {
            try
            {
                rfsaSession?.Close();
                rfsaSession = null;

                vst?.Close();
                rfsgSession?.Close();
                rfsgSession = null;
            }
            catch (Exception ex)
            {
                ShowError("Unable to Close Session, Reset the device.\n" + "Error : " + ex.Message);
                Application.Exit();
            }
        }

        private void ConfigureReferenceClock()
        {
            rfsaSession.Configuration.ReferenceClock.Source = ReferenceClockSource;
        }

        private void ConfigureSelfCalibration()
        {
            List<NIDCPower> m_DC = new List<NIDCPower>();

            if (cbxDCCalibration.Checked)
            {
                foreach (var _dc in lbxDCs.Items)
                {
                    m_DC.Add(new NIDCPower(_dc.ToString(), true, string.Empty));
                }
            }

            RfsaSelfCalibrationSteps validSteps;
            switch (SelfCalibrationOperation)
            {
                case 0:
                    double satemp = rfsaSession.DeviceCharacteristics.GetDeviceTemperature();
                    double sgtemp = rfsgSession.DeviceCharacteristics.DeviceTemperature;
                    TemperatureAlignFlag(satemp, 0);
                    lblSATemp.Text = string.Format("SA: {0:F4}C\r\nSG: {1:F4}C", satemp, sgtemp);
                    Application.DoEvents();

                    if (cbxDCCalibration.Checked)
                    {
                        foreach (var _dc in m_DC)
                        {
                            _dc.Calibration.Self.SelfCalibrate(string.Empty);
                        }
                    }

                    rfsaSession.Calibration.Self.SelfCalibrateRange(RfsaSelfCalibrationSteps.None, 1400e6, 6000e6, -60, 20);
                    rfsgSession.Calibration.Self.SelfCalibrateRange(RfsgSelfCalibrationSteps.OmitNone, 1400e6, 2700e6, -40, 0);

                    break;
                case 1:
                    //Passing an empty array will ensure NI-RFSA perform all Self Calibration steps.
                    rfsaSession.Calibration.Self.SelfCalibrate(RfsaSelfCalibrationSteps.PreselectorAlignment);
                    break;
                case 2:
                    // Queries NI-RFSA for the valid calibration steps.
                    // Passing this value to the niRFSA Self Cal VI ensures valid calibration steps are not repeated,
                    // thus only calibrating necessary steps
                    rfsaSession.Calibration.Self.IsSelfCalibrationValid(out validSteps);
                    rfsaSession.Calibration.Self.SelfCalibrate(validSteps);
                    break;
                case 3:
                    // IF Flatness Self Calibration can take up to 15 minutes.
                    rfsaSession.Calibration.Self.SelfCalibrate(RfsaSelfCalibrationSteps.IFFlatness);
                    break;
            }
        }

        public bool TemperatureAlignFlag(double EquipmentTemperature, byte site) //return true to force alignment
        {
            //FileInfo TempFile;
            StreamWriter swTempFile;
            string TempLogLocation = $@"C:\Avago.ATF.Common\Input\TemperatureLog_{site}.txt";

            //Create temperature file
            using (swTempFile = new StreamWriter(TempLogLocation, false))
            {
                swTempFile.WriteLine(EquipmentTemperature);
            }
            return true; //force cal
        }


        private void ChangeControlState(bool state)
        {
            this.cbxDCCalibration.Enabled = state;
            this.resourceNameComboBox.Enabled = state;
            this.clockSourceComboBox.Enabled = state;
            this.selfCalibrationComboBox.Enabled = state;
            this.startButton.Enabled = state;
        }

        private void startButton_Click(object sender, EventArgs e)
        {
            indicatorLabel.BackColor = Color.Gray;
            ChangeControlState(false);
            try
            {
                // Steps:
                //1. Open a new NI-RFSA session.
                //2. Configure the Reference clock.
                //3. Determine which self-calibration steps to omit.
                //4. Initiate self-calibration.
                //5. Close the NI-RFSA session.
                InitializeRfsaSession();
                ConfigureReferenceClock();
                ConfigureSelfCalibration();
                CloseSession();
                indicatorLabel.BackColor = Color.Green;
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
                CloseSession();
            }
            finally
            {
                ChangeControlState(true);
            }
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            CloseSession();
        }

        private static void ShowError(string message)
        {
            if (string.IsNullOrEmpty(message))
                message = "Unexpected Error";
            MessageBox.Show(message, "Error"); ;
        }

    }

    public static class RFMXExtension
    {

        public static void ConfigureDebugSettings(string aliasName, bool requestedValueDebugEnabled, bool requestedValueCBreakPointsEnabled)
        {
            const int noOfRetries = 100;
            const int msToWaitbeforeRetrying = 200;
            ResourceProperty isDebugSupportedProperty = ResourceProperty.RegisterSimpleType(typeof(bool), 0x10001000);
            ResourceProperty debugSessionConfigurationProperty = ResourceProperty.RegisterSimpleType(typeof(UInt32), 0x10002000);
            ResourceProperty usingCBreakpointsProperty = ResourceProperty.RegisterSimpleType(typeof(bool), 0x10003000);
            //Open a session to localhost
            SystemConfiguration session = new SystemConfiguration("");
            //Create a filter
            Filter devicefilter = new Filter(session, FilterMode.MatchValuesAll) { UserAlias = aliasName };
            //Find hardware based on given alias
            ResourceCollection resources = session.FindHardware(devicefilter);
            if (resources.Count == 0)
            {
                return;
                //throw new Exception("Error: No hardware found with the given alias!!!");
            }
            //Always use the device at index 0 to read and write the settings
            HardwareResourceBase hwResource = resources[0];
            bool isDebugSupported = Convert.ToBoolean(hwResource.GetPropertyValue(isDebugSupportedProperty));
            if (isDebugSupported)
            {
                hwResource.SetPropertyValue(debugSessionConfigurationProperty, Convert.ToUInt32(requestedValueDebugEnabled));
                hwResource.SetPropertyValue(usingCBreakpointsProperty, requestedValueCBreakPointsEnabled);
                //Save the changes
                bool requiresRestart = false;
                hwResource.SaveChanges(out requiresRestart);
                //Read back the saved change to confirm the settings bave been successfully applied.
                //Retry multiple times as it can take time for the settings to take effect
                //If there is a long time gap between changing the settings and Creating/Initializing
                //the RFmx session then re-try logic can be skipped.
                for (int i = 0; i < noOfRetries; i++) //Retry
                {
                    Object myobj = hwResource.GetPropertyValue(debugSessionConfigurationProperty);
                    bool value1 = Convert.ToBoolean(myobj);
                    myobj = hwResource.GetPropertyValue(usingCBreakpointsProperty);
                    bool value2 = Convert.ToBoolean(myobj);
                    if (value1 == requestedValueDebugEnabled && value2 == requestedValueCBreakPointsEnabled)
                        return;//Settings successfully applied
                    System.Threading.Thread.Sleep(msToWaitbeforeRetrying);//Wait for before retrying
                }
                throw new Exception("Error: Unable to update settings");
            }
            else
                throw new Exception("Error: Device does not support debugging");
        }
    }
}
