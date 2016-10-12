using System;
using System.Text;

using QlmLicenseLib;


namespace QLM
{
    public class LicenseValidator
    {

        private QlmLicense license = new QlmLicense();
        private string activationKey;
        private string computerKey;

        // To embed a trial key, set the defaultTrialKey to a trial license key
        protected string defaultTrialKey = string.Empty;

        private bool isEvaluation = false;
        private bool evaluationExpired = false;
        private int evaluationRemainingDays = -1;
        private bool wrongProductVersion = false;

        private bool checkIfLicenseIsRevoked = false;
        private bool checkIfComputerIsRegistered = false;
        private bool reactivateSubscription = false;

        /// <summary>
        /// Constructor initializes the license product definition
        /// </summary>
        public LicenseValidator()
        {
            license = new QlmLicense();

            // Always obfuscate your code. In particular, you should always obfuscate all arguments
            // of DefineProduct and the Public Key (i.e. encrypt all the string arguments).

            // If you are using the QLM License Wizard, you can load the product definition from the settings.xml file generated
            // by the Protect Your App Wizard.
            // To load the settings from the XML file, call the license.LoadSettings function.

            license.DefineProduct (1, "Demo", 1, 0, "DemoKey", "{24EAA3C1-3DD7-40E0-AEA3-D20AA17A6005}");
			license.PublicKey = "A59Jip0lt73Xig==";
			license.CommunicationEncryptionKey = "{B6163D99-F46A-4580-BB42-BF276A507A14}";
			license.DefaultWebServiceUrl = "http://quicklicensemanager.com/qlmdemov9/qlmservice.asmx";
			license.StoreKeysLocation = EStoreKeysTo.ERegistry;

            // If you are using QLM Professional, you should also set the communicationEncryptionKey property
            // The CommunicationEncryptionKey must match the value specified in the web.config file of the QLM License Server

            // Make sure that the StoreKeysLocation specified here is consistent with the one specified in the QLM .NET Control
            // If you are using the QlmLicenseWizard, you must set the StoreKeysLocation to EStoreKeysTo.ERegistry
        }

        /// <remarks>Call ValidateLicenseAtStartup when your application is launched. 
        /// If this function returns false, exit your application.
        /// </remarks>
        /// 
        /// <summary>
        /// Validates the license when the application starts up. 
        /// The first time a license key is validated successfully,
        /// it is stored in a hidden file on the system. 
        /// When the application is restarted, this code will load the license
        /// key from the hidden file and attempt to validate it again. 
        /// If it validates succesfully, the function returns true.
        /// If the license key is invalid, expired, etc, the function returns false.
        /// </summary>
        /// <param name="computerID">Unique Computer identifier</param>
        /// <param name="returnMsg">Error message returned, in case of an error</param>
        /// <returns>true if the license is OK.</returns>
        public virtual bool ValidateLicenseAtStartup(string computerID, ref bool needsActivation, ref string returnMsg)
        {
            returnMsg = string.Empty;
            needsActivation = false;

            string storedActivationKey = string.Empty;
            string storedComputerKey = string.Empty;

            license.ReadKeys(ref storedActivationKey, ref storedComputerKey);

            if (!String.IsNullOrEmpty(storedActivationKey))
            {
                activationKey = storedActivationKey;
            }

            if (!String.IsNullOrEmpty(storedComputerKey))
            {
                computerKey = storedComputerKey;
            }

            // To embed a trial key, set the defaultTrialKey to a trial license key
            if (String.IsNullOrEmpty(activationKey) && String.IsNullOrEmpty(computerKey))
            {
                if (String.IsNullOrEmpty(defaultTrialKey))
                {
                    return false;
                }
                else
                {
                    activationKey = defaultTrialKey;
                }
            }

            bool ret = ValidateLicense(activationKey, computerKey, computerID, ref needsActivation, ref returnMsg);

            if (ret == true)
            {
                // If the local license is valid, check on the server if it's valid as well.
                if (ValidateOnServer(computerID, ref returnMsg) == false)
                { 
                    return false;
                }
            }

            //
            // If a license has expired but then renewed on the server, reactivating the key will extend the client
            // with the new subscription period.
            //
            if ((wrongProductVersion || EvaluationExpired) && ReactivateSubscription)
            {
                ret = ReactivateKey(computerID);
            }

            return ret;

        }

        /// <remarks>Call this function in the dialog where the user enters the license key to validate the license.</remarks>
        /// <summary>
        /// Validates a license key. If you provide a computer key, the computer key is validated. 
        /// Otherwise, the activation key is validated. 
        /// If you are using machine bound keys (UserDefined), you can provide the computer identifier, 
        /// otherwise set the computerID to an empty string.
        /// </summary>
        /// <param name="activationKey">Activation Key</param>
        /// <param name="computerKey">Computer Key</param>
        /// <param name="computerID">Unique Computer identifier</param>
        /// <returns>true if the license is OK.</returns>
        public virtual bool ValidateLicense(string activationKey, string computerKey, string computerID, ref bool needsActivation, ref string returnMsg)
        {
            bool ret = false;

            needsActivation = false;
            isEvaluation = false;
            evaluationExpired = false;
            evaluationRemainingDays = -1;
            wrongProductVersion = false;

            string licenseKey = computerKey;

            if (String.IsNullOrEmpty(licenseKey))
            {
                licenseKey = activationKey;

                if (String.IsNullOrEmpty(licenseKey))
                {
                    return false;
                }
            }

            returnMsg = license.ValidateLicenseEx(licenseKey, computerID);

            int nStatus = (int)license.GetStatus();

            if (IsTrue(nStatus, (int)ELicenseStatus.EKeyInvalid) ||
                IsTrue(nStatus, (int)ELicenseStatus.EKeyProductInvalid) ||
                IsTrue(nStatus, (int)ELicenseStatus.EKeyMachineInvalid) ||
                IsTrue(nStatus, (int)ELicenseStatus.EKeyExceededAllowedInstances) ||
                IsTrue(nStatus, (int)ELicenseStatus.EKeyTampered))
            {
                // the key is invalid
                ret = false;
            }
            else if (IsTrue(nStatus, (int)ELicenseStatus.EKeyVersionInvalid))
            {
                wrongProductVersion = true;
                ret = false;
            }
            else if (IsTrue(nStatus, (int)ELicenseStatus.EKeyDemo))
            {
                isEvaluation = true;

                if (IsTrue(nStatus, (int)ELicenseStatus.EKeyExpired))
                {
                    // the key has expired
                    ret = false;
                    evaluationExpired = true;
                }
                else
                {
                    // the demo key is still valid
                    ret = true;
                    evaluationRemainingDays = license.DaysLeft;
                }
            }
            else if (IsTrue(nStatus, (int)ELicenseStatus.EKeyPermanent))
            {
                // the key is OK                
                ret = true;
            }

            if (ret == true)
            {

                if (license.LicenseType == ELicenseType.Activation)
                {
                    needsActivation = true;
                    ret = false;
                }
                else
                {
                    license.StoreKeys(activationKey, computerKey);
                }
            }

            return ret;

        }


        /// <summary>
        /// Check on the License Server if the license key has been revoked and/or if the license is illegal, i.e. not registered on the server
        /// </summary>
        /// <param name="computerID"></param>
        /// <param name="returnMsg"></param>
        /// <returns></returns>
        private bool ValidateOnServer(string computerID, ref string returnMsg)
        {
            bool ret = true;

            if (!this.CheckIfLicenseIsRevoked && !this.CheckIfComputerIsRegistered)
            {
                return true;
            }

            // First let's determine if the license key is expected to be found on the server
            // This is the case for keys that require activation.
            // So we will first check if the ActivationKey variable actually holds a key that requires activation
            // If we have an ActivationKey and a ComputerKey, we already called ValidateLicenseEx with the ComputerKey
            // Now we need to call it with the Activation Key

            ELicenseType licenseType = license.LicenseType;

            if (!String.IsNullOrEmpty(activationKey) && !String.IsNullOrEmpty(computerKey))
            {
                LicenseValidator lv = new LicenseValidator();
                returnMsg = lv.QlmLicenseObject.ValidateLicense(activationKey);
                licenseType = lv.QlmLicenseObject.LicenseType;
            }


            if (String.IsNullOrEmpty(license.DefaultWebServiceUrl) ||
                    String.IsNullOrEmpty(activationKey) ||
                     (licenseType != ELicenseType.Activation))

            {
                // If no URL to the web service was defined, we cannot do any validation with the server
                // If we do not have an Activation Key, we cannot check anything on the server
                return true;
            }

            DateTime serverDate;
            string response;
            if (license.Ping(string.Empty, out response, out serverDate) == false)
            {
                // we cannot connect to the server so we cannot do any validation with the server
                return true;
            }
            else
            {
                // Check if the time on the server is in sync with the time on this computer. 
                // Users may set the computer date back to circumvent expiry
                // The server date is converted to local time automatically
                TimeSpan ts = serverDate - DateTime.Now;
                if (ts.TotalHours > (double)24)
                {
                    returnMsg = String.Format("The time on this computer does not match the server time. The difference is {0} hours.", ts.TotalHours);
                    return false;
                }
            }

            if (this.checkIfLicenseIsRevoked && license.IsLicenseKeyRevoked(string.Empty, activationKey))
            {
                DeleteAllKeys();
                return false;
            }

            if (this.CheckIfComputerIsRegistered && license.IsIllegalComputer(string.Empty, activationKey, computerKey, computerID, Environment.MachineName, "5.0.00", out response))
            {
                ret = false;

                ILicenseInfo li = new LicenseInfo();
                license.ParseResults(response, ref li, ref returnMsg);

                // If the system is detected as illegal, it means that we found license keys on the local system but the keys are not registerd on the server.
                DeleteAllKeys();

                return false;

            }

            return ret;
        }

        /// <summary>
        /// Delete all license keys stored in the registry or on the file system
        /// </summary>
        public void DeleteAllKeys()
        {
            // the license was revoked, we need to remove the keys on this system.
            EStoreKeysTo saveLocation = license.StoreKeysLocation;

            try
            {
                // Remove keys stored on the file system
                license.StoreKeysLocation = EStoreKeysTo.EFile;
                license.DeleteKeys();

                // Remove keys stored in the registry
                license.StoreKeysLocation = EStoreKeysTo.ERegistry;
                license.DeleteKeys();
            }
            catch
            { }
            finally
            {
                // Restore the previous setting
                license.StoreKeysLocation = saveLocation;
            }
        }

        /// <summary>
        /// Reactivates a key - this is typically used to automatically get a subscription extension from the server
        /// </summary>
        /// <param name="computerID"></param>
        /// <param name="newComputerKey"></param>
        /// <returns></returns>
        private bool ReactivateKey(string computerID)
        {
            bool ret = false;
            string newComputerKey = string.Empty;

            // try to reactivate the license and see if it still expired
            string response = string.Empty;
            license.ActivateLicense(license.DefaultWebServiceUrl, ActivationKey, computerID,
                                        Environment.MachineName, "5.0.00", string.Empty, out response);

            ILicenseInfo licenseInfo = new LicenseInfo();
            string message = string.Empty;
            if (license.ParseResults(response, ref licenseInfo, ref message))
            {
                if (ComputerKey != licenseInfo.ComputerKey)
                {
                    newComputerKey = licenseInfo.ComputerKey;

                    bool needsActivation = false;
                    string returnMsg = string.Empty;

                    ret = ValidateLicense(activationKey, newComputerKey, computerID, ref needsActivation, ref returnMsg);

                    if (ret == true)
                    {
                        // The Computer Key has changed, update the local one
                        license.StoreKeys(activationKey, newComputerKey);
                    }
                }
                else
                {
                    ret = true;
                }
            }

            return ret;
        }

        /// <summary>
        /// Deletes the license keys stored on the computer. 
        /// </summary>
        public virtual void DeleteKeys()
        {
            license.DeleteKeys();
        }

        /// <summary>
        /// Returns the registered activation key
        /// </summary>
        public string ActivationKey
        {
            get
            {
                return activationKey;
            }
        }

        /// <summary>
        /// Returns the registered computer key
        /// </summary>
        public string ComputerKey
        {
            get
            {
                return computerKey;
            }
        }

        public bool IsEvaluation
        {
            get
            {
                return isEvaluation;
            }
        }

        public bool EvaluationExpired
        {
            get
            {
                return evaluationExpired;
            }
        }

        public int EvaluationRemainingDays
        {
            get
            {
                return evaluationRemainingDays;
            }
        }

        /// <summary>
        /// Returns the underlying license object
        /// </summary>
        public QlmLicense QlmLicenseObject
        {
            get
            {
                return license;
            }
        }

        public bool CheckIfLicenseIsRevoked
        {
            get
            {
                return checkIfLicenseIsRevoked;
            }

            set
            {
                checkIfLicenseIsRevoked = value;
            }
        }

        public bool CheckIfComputerIsRegistered
        {
            get
            {
                return checkIfComputerIsRegistered;
            }

            set
            {
                checkIfComputerIsRegistered = value;
            }
        }

        public bool ReactivateSubscription
        {
            get
            {
                return reactivateSubscription;
            }

            set
            {
                reactivateSubscription = value;
            }
        }

        public bool WrongProductVersion
        {
            get
            {
                return wrongProductVersion;
            }

            set
            {
                wrongProductVersion = value;
            }
        }

        /// <summary>
        /// Compares flags
        /// </summary>
        /// <param name="nVal1">Value 1</param>
        /// <param name="nVal2">Value 2</param>
        /// <returns></returns>
        private bool IsTrue(int nVal1, int nVal2)
        {
            if (((nVal1 & nVal2) == nVal1) || ((nVal1 & nVal2) == nVal2))
            {
                return true;
            }
            return false;
        }


    }
}
