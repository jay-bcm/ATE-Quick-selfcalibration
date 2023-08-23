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

using Jay.CommonLibrary.Addin;
using NationalInstruments.ModularInstruments.NIDCPower;
using NationalInstruments.ModularInstruments.NIRfsa;
using NationalInstruments.ModularInstruments.NIRfsg;
using NationalInstruments.ModularInstruments.SystemServices.DeviceServices;
using NationalInstruments.RFmx.InstrMX;
using NationalInstruments.SystemConfiguration;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SelfCalibration
{
    public partial class MainForm : Form
    {
        private NIRfsa rfsaSession;
        private NIRfsg rfsgSession;
        private RFmxInstrMX vst;
        public Dictionary<string, cDeviceInfo> DeviceCollection = new Dictionary<string, cDeviceInfo>();
        private double MaxFreq = 2700e6;

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
            {
                resourceNameComboBox.Items.Add(device.Name);
                DeviceInfo mq = device;
                DeviceCollection[mq.Name.ToUpper()] = new cDeviceInfo(mq.BusNumber, mq.ChassisNumber, mq.Model, mq.Name, mq.SerialNumber, mq.SlotNumber, mq.SocketNumber);
            }
            if (modularInstrumentsSystem.DeviceCollection.Count > 0)
                resourceNameComboBox.SelectedIndex = 0;

            ModularInstrumentsSystem _dcPowerLists = new ModularInstrumentsSystem("NI-DCPower");
            foreach (DeviceInfo _dc in _dcPowerLists.DeviceCollection)
            {
                lbxDCs.Items.Add(_dc.Name);
            }
        }

        public class cDeviceInfo
        {
            public int BusNumber, ChassisNumber;
            public string Model, Name, SerialNumber;
            public int SlotNumber, SocketNumber;

            /// <param name="busNumber"></param>
            /// <param name="chassisNumber"></param>
            /// <param name="model"></param>
            /// <param name="name">VisaAlias</param>
            /// <param name="serialNumber"></param>
            /// <param name="slotNumber"></param>
            /// <param name="socketNumber"></param>
            public cDeviceInfo(int busNumber, int chassisNumber, string model, string name, string serialNumber, int slotNumber, int socketNumber)
            {
                BusNumber = busNumber;
                ChassisNumber = chassisNumber;
                Model = model;
                Name = name;
                SerialNumber = serialNumber;
                SlotNumber = slotNumber;
                SocketNumber = socketNumber;
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

        #endregion Initial Configuration

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

        #endregion UI Gets

        private void InitializeRfsaSession()
        {
            CloseSession();
            RFMXExtension.ConfigureDebugSettings(ResourceName, false, false);

            rfsgSession = new NIRfsg(ResourceName, false, false, string.Empty);// "DriverSetup=Bitfile:NI-RFIC.lvbitx");
            vst = new RFmxInstrMX(ResourceName, string.Empty);// "DriverSetup=Bitfile:NI-RFIC.lvbitx");
            vst.DangerousGetNIRfsaHandle(out IntPtr niRfsaHandle);
            rfsaSession = new NIRfsa(niRfsaHandle);

            rfsaSession.DriverOperation.Warning += new System.EventHandler<RfsaWarningEventArgs>(SADriverOperationWarning);
            rfsgSession.DriverOperation.Warning += new System.EventHandler<RfsgWarningEventArgs>(SGDriverOperationWarning);

            MaxFreq = DeviceCollection[ResourceName.ToUpper()].Model == "NI PXIe-5842" ? 23000e6 : 6000e6;
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
            List<Task> threadRun = new List<Task>();

            if (cbxDCCalibration.Checked)
            {
                foreach (var _dc in lbxDCs.Items)
                {
                    m_DC.Add(new NIDCPower(_dc.ToString(), true, string.Empty));
                }
            }

            TemperatureAlignFlag(0, 0);
            Application.DoEvents();

            if (cbxDCCalibration.Checked)
            {
                foreach (var _dc in m_DC)
                {
                    threadRun.Add(Task.Factory.StartNew(() =>
                    {
                        _dc.Calibration.Self.SelfCalibrate(string.Empty);
                        _dc.Close();
                        _dc.Dispose();
                    }));
                }
            }

            RfsaSelfCalibrationSteps validSteps;
            switch (SelfCalibrationOperation)
            {
                case 0:

                    rfsaSession.Calibration.Self.SelfCalibrateRange(RfsaSelfCalibrationSteps.None, 1400e6, MaxFreq, -60, 20);
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

            Task.WaitAll(threadRun.ToArray());
        }

        public class TemperatureAlignData
        {
            public DateTime CalibrationDate { get; set; }
            public double PreviousTemperature_SA { get; set; }
            public double PreviousTemperature_SG { get; set; }
            public double EquipmentTemperature_SA { get; set; }
            public double EquipmentTemperature_SG { get; set; }
            public double ForceAlignmentDelta { get; set; }
            public string InstrumentInfo { get; set; }
            public byte Site { get; set; }
        }

        public class TemperatureAlignDataCollection
        {
            public List<TemperatureAlignData> Data;
        }

        public bool TemperatureAlignFlag(double EquipmentTemperature, byte site) //return true to force alignment
        {
            var vstdevice = DeviceCollection[ResourceName.ToUpper()];
            double satemp = rfsaSession.DeviceCharacteristics.GetDeviceTemperature();
            double sgtemp = rfsgSession.DeviceCharacteristics.DeviceTemperature;
            lblSATemp.Text = string.Format("SA: {0:F4}C\r\nSG: {1:F4}C", satemp, sgtemp);
            string TempLogLocation = $@"C:\Avago.ATF.Common\Input\TemperatureLog.json";
            string _InstrumentInfo = $"VST{site} = {vstdevice.Model}*{vstdevice.SerialNumber}; ";

            TemperatureAlignDataCollection deserializedCollection;
            DateTime dtCalibrationDate = DateTime.Now;

            //Directory.CreateDirectory(Directory.GetParent(TempLogLocation).FullName);
            bool TemperatureFileExist = (File.Exists(TempLogLocation) ? true : false);

            if (!TemperatureFileExist)
            {
                deserializedCollection = new TemperatureAlignDataCollection() { Data = new List<TemperatureAlignData>() };
                deserializedCollection.Data.Add(new TemperatureAlignData()
                {
                    CalibrationDate = dtCalibrationDate,
                    PreviousTemperature_SA = double.NaN,
                    PreviousTemperature_SG = double.NaN,
                    EquipmentTemperature_SA = satemp,
                    EquipmentTemperature_SG = sgtemp,
                    ForceAlignmentDelta = 1.0,
                    InstrumentInfo = _InstrumentInfo,
                    Site = site
                });

                string updatedJson = JsonConvert.SerializeObject(deserializedCollection, Formatting.Indented);
                File.WriteAllText(TempLogLocation, updatedJson);
            }
            else
            {
                // Read the JSON data from the file
                string json = File.ReadAllText(TempLogLocation);

                // Deserialize the JSON back to the SensorData object
                deserializedCollection = JsonConvert.DeserializeObject<TemperatureAlignDataCollection>(json);
                if (deserializedCollection == null) deserializedCollection = new TemperatureAlignDataCollection() { Data = new List<TemperatureAlignData>() };
                else if (deserializedCollection.Data == null) deserializedCollection.Data = new List<TemperatureAlignData>();

                var CurrentTemperatureAlignmentData = deserializedCollection.Data.Where(d => d.InstrumentInfo.CIvEquals(_InstrumentInfo) && d.Site == site);

                if (CurrentTemperatureAlignmentData.CountOrNull() == 0)
                {
                    deserializedCollection.Data.Add(new TemperatureAlignData()
                    {
                        CalibrationDate = dtCalibrationDate,
                        PreviousTemperature_SA = double.NaN,
                        PreviousTemperature_SG = double.NaN,
                        EquipmentTemperature_SA = satemp,
                        EquipmentTemperature_SG = sgtemp,
                        ForceAlignmentDelta = 1.0,
                        InstrumentInfo = _InstrumentInfo,
                        Site = site
                    });

                    string updatedJson = JsonConvert.SerializeObject(deserializedCollection, Formatting.Indented);
                    File.WriteAllText(TempLogLocation, updatedJson);
                }
                else
                {
                    var data = CurrentTemperatureAlignmentData.First();
                    var previousTemperature = data.EquipmentTemperature_SA;

                    //Force alignment if temperature delta > setting
                    {
                        data.CalibrationDate = dtCalibrationDate;
                        data.PreviousTemperature_SA = data.EquipmentTemperature_SA;
                        data.PreviousTemperature_SG = data.EquipmentTemperature_SG;
                        data.EquipmentTemperature_SA = satemp;
                        data.EquipmentTemperature_SG = sgtemp;
                        data.ForceAlignmentDelta = 1.0;
                        data.InstrumentInfo = data.InstrumentInfo;
                        data.Site = site;

                        string updatedJson = JsonConvert.SerializeObject(deserializedCollection, Formatting.Indented);
                        File.WriteAllText(TempLogLocation, updatedJson);
                    }
                }
            }

            return true;
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