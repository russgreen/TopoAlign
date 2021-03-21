using Autodesk.Revit.UI;
using RestSharp;
using RestSharp.Serialization.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace TopoAlign
{
    public static class CheckEntitlement
    {
        //Set values specific to the environment
        public const string baseApiUrl = @"https://apps.autodesk.com/"; 
            
        //App ID
        public const string appId = @"7412914718855875408";

        public static bool LicenseCheck(Autodesk.Revit.ApplicationServices.Application app)
        {
            string userId = string.Empty;
            bool isValid;
            DateTime checkDate;

            using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Archisoft\TopoAlign", true))
            {
                if (key == null)
                {
                    Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Archisoft\TopoAlign", true);
                }

                userId = key.GetValue("UserID", string.Empty).ToString();

                if (userId != string.Empty)
                {
                    isValid = bool.Parse(Cypher.DecryptString(key.GetValue("IsValid").ToString(), userId));
                    checkDate = DateTime.Parse(Cypher.DecryptString(key.GetValue("Checked").ToString(), userId));

                    if(isValid == true)
                    {
                        // check if we need to re-validate (every 30days)
                        if (DateTime.Now.Subtract(checkDate).Days > 30)
                        {
                            if (CheckLogin(userId, app) == true)
                            {
                                // record in the details in the registry
                                key.SetValue("UserID", userId, Microsoft.Win32.RegistryValueKind.String);
                                key.SetValue("IsValid", Cypher.EncryptData(true.ToString(), userId), Microsoft.Win32.RegistryValueKind.String);
                                key.SetValue("Checked", Cypher.EncryptData(DateTime.Now.ToString(), userId), Microsoft.Win32.RegistryValueKind.String);
                            }
                            else
                            {
                                return false;
                            }
                        }
                    }
                    else if (CheckLogin(userId, app) == true)
                    {
                        // record in the details in the registry
                        key.SetValue("UserID", userId, Microsoft.Win32.RegistryValueKind.String);
                        key.SetValue("IsValid", Cypher.EncryptData(true.ToString(), userId), Microsoft.Win32.RegistryValueKind.String);
                        key.SetValue("Checked", Cypher.EncryptData(DateTime.Now.ToString(), userId), Microsoft.Win32.RegistryValueKind.String);
                    }
                    else
                    {
                        return false;
                    }
                }
                else if (CheckLogin(userId, app) == true)
                {
                    // record in the details in the registry
                    key.SetValue("UserID", userId, Microsoft.Win32.RegistryValueKind.String);
                    key.SetValue("IsValid", Cypher.EncryptData(true.ToString(), userId), Microsoft.Win32.RegistryValueKind.String);
                    key.SetValue("Checked", Cypher.EncryptData(DateTime.Now.ToString(), userId), Microsoft.Win32.RegistryValueKind.String);
                }
                else
                {
                    return false;
                }
            }

            return true;
        }

        private static bool CheckLogin(string userId, Autodesk.Revit.ApplicationServices.Application app)
        {
            // Check to see if the user is logged in.
            if (!Autodesk.Revit.ApplicationServices.Application.IsLoggedIn)
            {
                TaskDialog.Show("TopoAlign addin license", "Please login to Autodesk 360 first\n");
                return false;
            }

            // Get the user id, and check entitlement 
            userId = app.LoginUserId;
            bool isValid = Entitlement(userId);
            if (isValid == false)
            {
                TaskDialog.Show("TopoAlign addin license", "You do not appear to have a valid license to use this addin. Please contact the author via the app store\n");
                return false;
            }

            return true;
        }

        ///============================================================
        /// URL: https://apps.autodesk.com/webservices/checkentitlement
        ///
        /// Method: GET
        ///
        /// Sample response
        /// {
        /// "UserId":"2N5FMZW9CCED",
        /// "AppId":"2024453975166401172",
        /// "IsValid":false,
        /// "Message":"Ok"
        /// }
        ///============================================================
        private static bool Entitlement(string userId)
        {
            //REST API call for the entitlement API.

            //(1) Build request
            var client = new RestClient();
            client.BaseUrl = new System.Uri(baseApiUrl);

            //Set resource/end point
            var request = new RestRequest();
            request.Resource = "webservices/checkentitlement";
            request.Method = Method.GET;

            //Add parameters
            request.AddParameter("userid", userId);
            request.AddParameter("appid", appId);

            //(2) Execute request and get response
            IRestResponse response = client.Execute(request);

            //Get the entitlement status.
            bool isValid = false;
            if (response.StatusCode == HttpStatusCode.OK)
            {
                JsonDeserializer deserial = new JsonDeserializer();
                EntitlementResponse entitlementResponse =
                deserial.Deserialize<EntitlementResponse>(response);
                isValid = entitlementResponse.IsValid;
            }

            return isValid;
        }
    }

    [Serializable]
    public class EntitlementResponse
    {
        public string UserId { get; set; }
        public string AppId { get; set; }
        public bool IsValid { get; set; }
        public string Message { get; set; }
    }

}
