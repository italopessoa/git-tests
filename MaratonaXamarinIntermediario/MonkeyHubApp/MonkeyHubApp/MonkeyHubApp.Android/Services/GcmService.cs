﻿using System;
using System.Collections.Generic;
using System.Text;

using Android.App;
using Android.Content;
using Gcm.Client;
using Microsoft.WindowsAzure.MobileServices;
using Android.Util;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using Android.Media;
using Android.Support.V4.App;
//https://social.msdn.microsoft.com/Forums/pt-BR/749bb39c-bb18-4434-b95c-22309fbcf1d2/uwpmvvmazure-cant-insert-data-to-easy-table-on-azure?forum=azuremobile
//https://github.com/SaschaDittmann/Xamarin.NotificationHub/issues/9
[assembly: Permission(Name = "@PACKAGE_NAME@.permission.C2D_MESSAGE")]
[assembly: UsesPermission(Name = "@PACKAGE_NAME@.permission.C2D_MESSAGE")]
[assembly: UsesPermission(Name = "com.google.android.c2dm.permission.RECEIVE")]
[assembly: UsesPermission(Name = "android.permission.INTERNET")]
[assembly: UsesPermission(Name = "android.permission.WAKE_LOCK")]
//GET_ACCOUNTS is only needed for android versions 4.0.3 and below
[assembly: UsesPermission(Name = "android.permission.GET_ACCOUNTS")]
namespace MonkeyHubApp.Droid.Services
{
    [BroadcastReceiver(Permission = Gcm.Client.Constants.PERMISSION_GCM_INTENTS)]
    [IntentFilter(new string[] { Gcm.Client.Constants.INTENT_FROM_GCM_MESSAGE }, Categories = new string[] { "@PACKAGE_NAME@" })]
    [IntentFilter(new string[] { Gcm.Client.Constants.INTENT_FROM_GCM_REGISTRATION_CALLBACK }, Categories = new string[] { "@PACKAGE_NAME@" })]
    [IntentFilter(new string[] { Gcm.Client.Constants.INTENT_FROM_GCM_LIBRARY_RETRY }, Categories = new string[] { "@PACKAGE_NAME@" })]
    public class PushHandlerBroadcastReceiver : GcmBroadcastReceiverBase<GcmService>
    {
        private static string[] sENDER_IDS = new string[] { "" };

        public static string[] SENDER_IDS { get => sENDER_IDS; }
    }

    [Service]
    public class GcmService : GcmServiceBase
    {
        private const string mobileApp = "";
        MobileServiceClient client = new MobileServiceClient($"http://{mobileApp}.azurewebsites.net/");

        public static string RegistrationID { get; private set; }


        public GcmService() 
            : base(PushHandlerBroadcastReceiver.SENDER_IDS)
        {

        }

        protected override void OnError(Context context, string errorId)
        {
            Log.Error("PushHandlerBroadcastReceiver", $"GCM Error: {errorId}");
        }

        protected override void OnMessage(Context context, Intent intent)
        {
            Log.Info("PushHandlerBroadcastReceiver","GCM Message Received!");

            var msg = new StringBuilder();

            if(intent?.Extras != null)
            {
                foreach (var key in intent.Extras.KeySet())
                {
                    msg.AppendLine($"{key}={intent.Extras.Get(key).ToString()}");
                }

                //store the message
                var pref = GetSharedPreferences(context.PackageName,FileCreationMode.Private);
                var edit = pref.Edit();
                edit.PutString("last_msg",msg.ToString());
                edit.Commit();

                string message = intent?.Extras?.GetString("message");

                if (!string.IsNullOrEmpty(message))
                {
                    CreateNotification("Push", message);
                    return;
                }

                string msg2 = intent?.Extras?.GetString("msg");

                if (!string.IsNullOrEmpty(msg2))
                {
                    CreateNotification("New hub message!", msg2);
                    return;
                }

                CreateNotification("Unknown message details", msg.ToString());
            }
        }

        private void CreateNotification(string title, string desc)
        {
            //create notification
            var notificationManager = GetSystemService(Context.NotificationService) as NotificationManager;

            //create an intent to show ui
            var uiIntent = new Intent(this, typeof(MainActivity));

            //use notification builder
            NotificationCompat.Builder builder = new NotificationCompat.Builder(this);

            //Create the notification
            //we use the pending intent, passing our ui intent over which will get called
            //when the notification is tapped.
            var notification = builder.SetContentIntent(PendingIntent.GetActivity(this, 0, uiIntent, 0))

               .SetSmallIcon(Android.Resource.Drawable.SymActionEmail)
                .SetTicker(title)
                .SetContentTitle(title)
                .SetContentText(desc)
                .SetSound(RingtoneManager.GetDefaultUri(RingtoneType.Notification))//set the notification sound
                .SetPriority((int)NotificationPriority.Max)
                .SetVibrate(new long[0])
                .SetAutoCancel(true).Build();//Auto cancel will remove the notification once the user touches it

            notificationManager.Notify(1, notification);
        }

        protected override void OnRegistered(Context context, string registrationId)
        {
            Log.Verbose("PushHandlerBroadcastReceiver",$"GCM Registered: {registrationId}");

            RegistrationID = registrationId;

            var push = client.GetPush();

            MainActivity.CurrentActivity.RunOnUiThread(() => Register(push, null));
        }

        public async void Register(Push push, IEnumerator<string> tags)
        {
            try
            {
                const string templateBodyGCM = "{\"data\":{\"message\":\"$(mesageParam)\"}}";

                JObject templates = new JObject
                {
                    ["genericMessage"] = new JObject
                    {
                        { "body", templateBodyGCM }
                    }
                };

                await push.RegisterAsync(RegistrationID, templates);

                Log.Info("Push Installation Id",push.InstallationId.ToString());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);

                Debugger.Break();
            }
        }

        protected override void OnUnRegistered(Context context, string registrationId)
        {
            Log.Error("PushHandlerBroadcastReceiver",$"Unregistered RegistrationId: {registrationId}");
        }
    }
}