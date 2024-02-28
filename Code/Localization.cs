// <copyright file="Localization.cs" company="algernon (K. Algernon A. Sheppard)">
// Copyright (c) algernon (K. Algernon A. Sheppard). All rights reserved.
// Licensed under the Apache Licence, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// See LICENSE.txt file in the project root for full license information.
// </copyright>

namespace ImageOverlay
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using Colossal.Localization;
    using Colossal.Logging;
    using Game.Modding;
    using Game.SceneFlow;

    /// <summary>
    /// Translation handling.
    /// </summary>
    public static class Localization
    {
        /// <summary>
        /// Loads settings translations from tab-separated l10n file.
        /// </summary>
        /// <param name="settings">Settings file to use.</param>
        /// <param name="log">Log to use.</param>
        public static void LoadTranslations(ModSetting settings, ILog log)
        {
            Assembly thisAssembly = Assembly.GetExecutingAssembly();
            string[] resourceNames = thisAssembly.GetManifestResourceNames();

            try
            {
                foreach (string localeID in GameManager.instance.localizationManager.GetSupportedLocales())
                {
                    string resourceName = "ImageOverlay.l10n." + localeID + ".csv";
                    if (resourceNames.Contains(resourceName))
                    {
                        try
                        {
                            log.Info($"reading translation file {resourceName}");

                            // Read embedded file.
                            using StreamReader reader = new (thisAssembly.GetManifestResourceStream(resourceName));
                            {
                                Dictionary<string, string> languageDict = new ();

                                while (!reader.EndOfStream)
                                {
                                    // Skip empty lines.
                                    string line = reader.ReadLine();
                                    if (!string.IsNullOrWhiteSpace(line))
                                    {
                                        string[] columns = line.Split('\t');

                                        if (columns.Length > 1)
                                        {
                                            // Trim quotation marks.
                                            languageDict.Add(GenerateOptionsKey(columns[0].Trim('"'), settings), columns[1].Trim('"'));
                                        }
                                    }
                                }

                                // Add translations to game locales.
                                log.Info("adding translation for " + localeID);
                                GameManager.instance.localizationManager.AddSource(localeID, new MemorySource(languageDict));
                            }
                        }
                        catch (Exception e)
                        {
                            // Don't let a single failure stop us.
                            log.Error(e, $"exception reading localization for locale {localeID}");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                log.Error(e, "exception reading settings localization file");
            }
        }

        /// <summary>
        /// Generates a settings option localization key.
        /// </summary>
        /// <param name="translationKey">Translation key key.</param>
        /// <param name="settings">Settings instance.</param>
        /// <returns>Full option localization key.</returns>
        private static string GenerateOptionsKey(string translationKey, ModSetting settings)
        {
            int divider = translationKey.IndexOf(':');
            string context = translationKey.Remove(divider);
            string key = translationKey.Substring(divider + 1);

            Mod.Instance.Log.Info($"generating options context {context} and key {key} from {translationKey}");

            return context switch
            {
                "Options.OPTION" => settings.GetOptionLabelLocaleID(key),
                "Options.OPTION_DESCRIPTION" => settings.GetOptionDescLocaleID(key),
                "Options.WARNING" => settings.GetOptionWarningLocaleID(key),
                _ => settings.GetSettingsLocaleID(),
            };
        }
    }
}
