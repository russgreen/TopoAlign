﻿using Autodesk.Revit.UI;
using RestSharp;

namespace TopoAlign;

public static class CheckEntitlement
{
    //Set values specific to the environment
    public const string baseApiUrl = @"https://apps.autodesk.com";

    //App ID
    public const string appId = @"7412914718855875408";

    private static readonly string _domain = Environment.UserDomainName;
    private static string _userId = string.Empty;
    private static bool _isValid;

    public static bool LicenseCheck(Autodesk.Revit.ApplicationServices.Application app)
    {
#if DEBUG
        return true;
#endif
        DateTime checkDate = DateTime.Now;

        //check if its an ECE user....they get a free pass
        if(_domain.ToLower() == ("ece"))
        {
            return true;
        }

        Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\RGTools\TopoAlign", true);

        if (key == null)
        {
            Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"SOFTWARE\RGTools\TopoAlign", true);
            key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\RGTools\TopoAlign", true);
        }

        _userId = key.GetValue("UserID", string.Empty).ToString();

        if(_userId != string.Empty) 
        {
            try
            {
                _isValid = bool.Parse(Cypher.DecryptString(key.GetValue("IsValid").ToString(), _userId));
                checkDate = DateTime.Parse(Cypher.DecryptString(key.GetValue("Checked").ToString(), _userId));
            }
            catch
            {
                _isValid = false;
            }
                          

            if (_isValid)
            {
                // check if we need to re-validate (every 30days)
                if (DateTime.Now.Subtract(checkDate).Days > 30)
                {
                    if (CheckLogin( app))
                    {
                        // record in the details in the registry
                        key.SetValue("UserID", _userId, Microsoft.Win32.RegistryValueKind.String);
                        key.SetValue("IsValid", Cypher.EncryptData(true.ToString(), _userId), Microsoft.Win32.RegistryValueKind.String);
                        key.SetValue("Checked", Cypher.EncryptData(DateTime.Now.ToString(), _userId), Microsoft.Win32.RegistryValueKind.String);
                    }
                    else
                    {
                        return false;
                    }
                }
            }
            else if (CheckLogin(app))
            {
                // record in the details in the registry
                key.SetValue("UserID", _userId, Microsoft.Win32.RegistryValueKind.String);
                key.SetValue("IsValid", Cypher.EncryptData(true.ToString(), _userId), Microsoft.Win32.RegistryValueKind.String);
                key.SetValue("Checked", Cypher.EncryptData(DateTime.Now.ToString(), _userId), Microsoft.Win32.RegistryValueKind.String);
            }
            else
            {
                return false;
            }
        }
        else if (CheckLogin(app))
        {
            // record in the details in the registry
            key.SetValue("UserID", _userId, Microsoft.Win32.RegistryValueKind.String);
            key.SetValue("IsValid", Cypher.EncryptData(true.ToString(), _userId), Microsoft.Win32.RegistryValueKind.String);
            key.SetValue("Checked", Cypher.EncryptData(DateTime.Now.ToString(), _userId), Microsoft.Win32.RegistryValueKind.String);
        }
        else
        {
            return false;
        }

        return true;
    }

    private static bool CheckLogin(Autodesk.Revit.ApplicationServices.Application app)
    {
        // Check to see if the user is logged in.
        if (!Autodesk.Revit.ApplicationServices.Application.IsLoggedIn)
        {
            Autodesk.Revit.UI.TaskDialog.Show("TopoAlign addin license", "Please login to your Autodesk account first.  You must use the same account in the app store as you are logged into Revit");
            return false;
        }

        // Get the user id, and check entitlement 
        _userId = app.LoginUserId;

        bool isValid = Entitlement(_userId);

        if (isValid == false)
        {
            Autodesk.Revit.UI.TaskDialog.Show("TopoAlign addin license", "You do not appear to have a valid license to use this addin. Please contact the author via the app store\n");
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
        var client = new RestClient
        {
            BaseUrl = new Uri(baseApiUrl)
        };

        //Set resource/end point
        var request = new RestRequest
        {
            Resource = "webservices/checkentitlement",
            Method = Method.GET
        };

        //Add parameters
        request.AddParameter("userid", userId);
        request.AddParameter("appid", appId);

        //(2) Execute request and get response
        IRestResponse<EntitlementResult> response = client.Execute<EntitlementResult>(request);

        // Get the auth token. 
        bool isValid = false;
        if (response.Data != null && response.Data.IsValid)
        {
            isValid = true;
        }

        return isValid;
    }
}

[Serializable]
public class EntitlementResult
{
    public string UserId { get; set; }
    public string AppId { get; set; }
    public bool IsValid { get; set; }
    public string Message { get; set; }
}

