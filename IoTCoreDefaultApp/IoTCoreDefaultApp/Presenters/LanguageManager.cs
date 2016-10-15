﻿using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Globalization;
using Windows.System;
using Windows.System.UserProfile;

namespace IoTCoreDefaultApp
{
    public class LanguageManager
    {
        private Dictionary<string, string> displayNameToLanguageMap;
        public IReadOnlyList<string> LanguageDisplayNames
        {
            get;
            set;
        }

        public LanguageManager()
        {
            displayNameToLanguageMap = ApplicationLanguages.Languages.Select(tag =>
            {
                var lang = new Language(tag);
                return new KeyValuePair<string, string>(lang.DisplayName, lang.LanguageTag);
            }).ToDictionary(keyitem => keyitem.Key, valueItem => valueItem.Value);

            LanguageDisplayNames = displayNameToLanguageMap.Keys.ToList();
        }

        public void UpdateLanguage(string displayName)
        {
            ApplicationLanguages.PrimaryLanguageOverride = GetLanguageTagFromDisplayName(displayName);
        }

        private string GetLanguageTagFromDisplayName(string displayName)
        {
            string newLang;
            displayNameToLanguageMap.TryGetValue(displayName, out newLang);

            if (newLang == null)
            {
                throw new ArgumentException("Failed to get language tag for "+ displayName);
            }

            return newLang;
        }

        public static string GetCurrentLanguageDisplayName()
        {
            var lang = new Language(GlobalizationPreferences.Languages[0]);

            return lang.DisplayName;
        }

        public static IReadOnlyList<string> GetSupportedTimeZones()
        {
            return TimeZoneSettings.SupportedTimeZoneDisplayNames;
        }

        public static string GetCurrentTimeZone()
        {
            return TimeZoneSettings.CurrentTimeZoneDisplayName;
        }

        public static void ChangeTimeZone(string timeZone)
        {
            if (!TimeZoneSettings.CanChangeTimeZone)
            {
                return;
            }

            if (!TimeZoneSettings.SupportedTimeZoneDisplayNames.Contains(timeZone))
            {
                throw new ArgumentException("Failed to change timezone to " + timeZone);
            }

            TimeZoneSettings.ChangeTimeZoneByDisplayName(timeZone);

            // "Workaround" to flush TimeZoneInfo cache. Yes, this really works.
            TimeZoneInfo.ConvertTime(DateTime.MinValue, TimeZoneInfo.Local); 
        }
    }
}
