using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEditor;
using System.Linq;

namespace GitHub.Unity
{
    public struct TheVersion : IComparable<TheVersion>
    {
        const string versionRegex = @"(?<major>\d+)(\.?(?<minor>[^.]+))?(\.?(?<patch>[^.]+))?(\.?(?<build>.+))?";
        const int PART_COUNT = 4;

        [NotSerialized] private int major;
        [NotSerialized] public int Major { get { Initialize(version); return major; } }
        [NotSerialized] private int minor;
        [NotSerialized] public int Minor { get { Initialize(version); return minor; } }
        [NotSerialized] private int patch;
        [NotSerialized] public int Patch { get { Initialize(version); return patch; } }
        [NotSerialized] private int build;
        [NotSerialized] public int Build { get { Initialize(version); return build; } }
        [NotSerialized] private string special;
        [NotSerialized] public string Special { get { Initialize(version); return special; } }
        [NotSerialized] private bool isAlpha;
        [NotSerialized] public bool IsAlpha { get { Initialize(version); return isAlpha; } }
        [NotSerialized] private bool isBeta;
        [NotSerialized] public bool IsBeta { get { Initialize(version); return isBeta; } }
        [NotSerialized] private bool isUnstable;
        [NotSerialized] public bool IsUnstable { get { Initialize(version); return isUnstable; } }

        [NotSerialized] private int[] intParts;
        [NotSerialized] private string[] stringParts;
        [NotSerialized] private int parts;
        [NotSerialized] private bool initialized;

        private string version;

        private static readonly Regex regex = new Regex(versionRegex);

        public static TheVersion Parse(string version)
        {
            Guard.ArgumentNotNull(version, "version");
            TheVersion ret = default(TheVersion);
            ret.Initialize(version);
            return ret;
        }

        private void Initialize(string theVersion)
        {
            if (initialized)
                return;

            this.version = theVersion;

            isAlpha = false;
            isBeta = false;
            major = 0;
            minor = 0;
            patch = 0;
            build = 0;
            special = null;
            parts = 0;

            intParts = new int[PART_COUNT];
            stringParts = new string[PART_COUNT];

            for (var i = 0; i < PART_COUNT; i++)
                stringParts[i] = intParts[i].ToString();

            var match = regex.Match(version);
            if (!match.Success)
                throw new ArgumentException("Invalid version: " + version, "theVersion");

            major = int.Parse(match.Groups["major"].Value);
            intParts[0] = major;
            parts = 1;

            var minorMatch = match.Groups["minor"];
            var patchMatch = match.Groups["patch"];
            var buildMatch = match.Groups["build"];

            if (minorMatch.Success)
            {
                parts++;
                if (!int.TryParse(minorMatch.Value, out minor))
                {
                    special = minorMatch.Value;
                    stringParts[parts - 1] = special;
                }
                else
                {
                    intParts[parts - 1] = minor;

                    if (patchMatch.Success)
                    {
                        parts++;
                        if (!int.TryParse(patchMatch.Value, out patch))
                        {
                            special = patchMatch.Value;
                            stringParts[parts - 1] = special;
                        }
                        else
                        {
                            intParts[parts - 1] = patch;

                            if (buildMatch.Success)
                            {
                                parts++;
                                if (!int.TryParse(buildMatch.Value, out build))
                                {
                                    special = buildMatch.Value;
                                    stringParts[parts - 1] = special;
                                }
                                else
                                {
                                    intParts[parts - 1] = build;
                                }
                            }
                        }
                    }
                }
            }

            isUnstable = special != null;
            if (isUnstable)
            {
                isAlpha = special.IndexOf("alpha") >= 0;
                isBeta = special.IndexOf("beta") >= 0;
            }
            initialized = true;
        }

        public override string ToString()
        {
            return version;
        }

        public int CompareTo(TheVersion other)
        {
            if (this > other)
                return 1;
            if (this == other)
                return 0;
            return -1;
        }

        public override int GetHashCode()
        {
            int hash = 17;
            hash = hash * 23 + Major.GetHashCode();
            hash = hash * 23 + Minor.GetHashCode();
            hash = hash * 23 + Patch.GetHashCode();
            hash = hash * 23 + Build.GetHashCode();
            hash = hash * 23 + (Special != null ? Special.GetHashCode() : 0);
            return hash;
        }

        public override bool Equals(object obj)
        {
            if (obj is TheVersion)
                return Equals((TheVersion)obj);
            return false;
        }

        public bool Equals(TheVersion other)
        {
            return this == other;
        }

        public static bool operator==(TheVersion lhs, TheVersion rhs)
        {
            if (lhs.version == rhs.version)
                return true;
            return
                (lhs.Major == rhs.Major) &&
                (lhs.Minor == rhs.Minor) &&
                (lhs.Patch == rhs.Patch) &&
                (lhs.Build == rhs.Build) &&
                (lhs.Special == rhs.Special);
        }

        public static bool operator!=(TheVersion lhs, TheVersion rhs)
        {
            return !(lhs == rhs);
        }

        public static bool operator>(TheVersion lhs, TheVersion rhs)
        {
            if (lhs.version == rhs.version)
                return false;
            if (lhs.version == null)
                return false;
            if (rhs.version == null)
                return true;

            for (var i = 0; i < PART_COUNT; i++)
            {
                if (lhs.intParts[i] != rhs.intParts[i])
                    return lhs.intParts[i] > rhs.intParts[i];
            }

            for (var i = 1; i < PART_COUNT; i++)
            {
                if (lhs.stringParts[i] != rhs.stringParts[i])
                {
                    return GreaterThan(lhs.stringParts[i], rhs.stringParts[i]);
                }
            }
            return false;
        }

        public static bool operator<(TheVersion lhs, TheVersion rhs)
        {
            return !(lhs > rhs);
        }

        public static bool operator>=(TheVersion lhs, TheVersion rhs)
        {
            return lhs > rhs || lhs == rhs;
        }

        public static bool operator<=(TheVersion lhs, TheVersion rhs)
        {
            return lhs < rhs || lhs == rhs;
        }

        private static bool GreaterThan(string lhs, string rhs)
        {
            var lhsNonDigitPos = IndexOfFirstNonDigit(lhs);
            var rhsNonDigitPos = IndexOfFirstNonDigit(rhs);

            var lhsNumber = -1;
            if (lhsNonDigitPos > -1)
            {
                lhsNumber = int.Parse(lhs.Substring(0, lhsNonDigitPos));
            }
            else
            {
                int.TryParse(lhs, out lhsNumber);
            }

            var rhsNumber = -1;
            if (rhsNonDigitPos > -1)
            {
                rhsNumber = int.Parse(rhs.Substring(0, rhsNonDigitPos));
            }
            else
            {
                int.TryParse(rhs, out rhsNumber);
            }

            if (lhsNumber != rhsNumber)
                return lhsNumber > rhsNumber;

            return lhs.Substring(lhsNonDigitPos > -1 ? lhsNonDigitPos : 0).CompareTo(rhs.Substring(rhsNonDigitPos > -1 ? rhsNonDigitPos : 0)) > 0;
        }

        private static int IndexOfFirstNonDigit(string str)
        {
            for (var i = 0; i < str.Length; i++)
            {
                if (!char.IsDigit(str[i]))
                {
                    return i;
                }
            }
            return -1;
        }
    }

    public class Package
    {
        private string version;
        public string Url { get; set; }
        public string ReleaseNotes { get; set; }
        public string ReleaseNotesUrl { get; set; }
        public string Message { get; set; }
        [NotSerialized] public TheVersion Version { get { return TheVersion.Parse(version); } set { version = value.ToString(); } }
    }

    [Serializable]
    class GUIPackage
    {
        [SerializeField] private string version;
        [SerializeField] private string url;
        [SerializeField] private string releaseNotes;
        [SerializeField] private string releaseNotesUrl;
        [SerializeField] private string message;

        [NonSerialized] private Package package;
        public Package Package
        {
            get
            {
                if (package == null)
                {
                    package = new Package
                    {
                        Version = TheVersion.Parse(version),
                        Url = url,
                        ReleaseNotes = releaseNotes,
                        ReleaseNotesUrl = releaseNotesUrl,
                        Message = message
                    };
                }
                return package;
            }
        }

        public GUIPackage()
        {}

        public GUIPackage(Package package)
        {
            version = package.Version.ToString();
            url = package.Url;
            releaseNotes = package.ReleaseNotes;
            releaseNotesUrl = package.ReleaseNotesUrl;
            message = package.Message;
        }
    }

    public class UpdateCheckWindow :  EditorWindow
    {
        public const string UpdateFeedUrl =
#if DEBUG
        "http://localhost:55555/unity/latest.json"
#else
        "https://ghfvs-installer.github.com/unity/latest.json"
#endif
        ;

        public static void CheckForUpdates()
        {
            var savedReminderDate = EntryPoint.ApplicationManager.UserSettings.Get<string>(Constants.UpdateReminderDateKey);
            if (savedReminderDate.ToDateTimeOffset().Date > DateTimeOffset.Now.Date)
                return;

            var download = new DownloadTask(TaskManager.Instance.Token, EntryPoint.Environment.FileSystem, UpdateFeedUrl, EntryPoint.Environment.UserCachePath);
            download.OnEnd += (thisTask, result, success, exception) =>
            {
                if (success)
                {
                    try
                    {
                        var package = result.ReadAllText().FromJson<Package>(lowerCase: true, onlyPublic: false);
                        var current = TheVersion.Parse(ApplicationInfo.Version);
                        var versionsToSkip = EntryPoint.ApplicationManager.UserSettings.Get<TheVersion[]>(Constants.SkipVersionKey);
                        var newVersion = package.Version;

                        if (newVersion > current && (versionsToSkip != null && !versionsToSkip.Any(x => x == newVersion)))
                        {
                            TaskManager.Instance.RunInUI(() =>
                            {
                                NotifyOfNewUpdate(current, package);
                            });
                        }
                    }
                    catch(Exception ex)
                    {
                        Debug.LogError(ex);
                    }
                }
            };
            download.Start();
        }

        private static void NotifyOfNewUpdate(TheVersion currentVersion, Package package)
        {
            var window = GetWindowWithRect<UpdateCheckWindow>(new Rect(100, 100, 500, 400), true, windowTitle);
            window.Initialize(currentVersion, package);
            window.Show();
        }

        private const string windowTitle = "GitHub for Unity Update Check";
        private const string newUpdateMessage = "There is a new version of GitHub for Unity available.\n\nCurrent version is {0}\nNew version is {1}";
        private const string skipThisVersionMessage = "Skip new version";
        private const string remingMeTomorrowMessage = "Remind me tomorrow";
        private const string downloadNewVersionMessage = "Download new version";
        private const string browseReleaseNotes = "Browse the release notes";

        private static GUIContent guiLogo;
        private static GUIContent guiNewUpdate;
        private static GUIContent guiPackageReleaseNotes;
        private static GUIContent guiPackageMessage;
        private static GUIContent guiSkipThisVersion;
        private static GUIContent guiRemindMeTomorrow;
        private static GUIContent guiDownloadNewVersion;
        private static GUIContent guiBrowseReleaseNotes;

        [SerializeField] private GUIPackage package;
        [SerializeField] private TheVersion currentVersion;
        [SerializeField] private Vector2 scrollPos;
        [SerializeField] private bool hasReleaseNotes;
        [SerializeField] private bool hasReleaseNotesUrl;
        [SerializeField] private bool hasMessage;

        private void Initialize(TheVersion current, Package newPackage)
        {
            
            package = new GUIPackage(newPackage);
            currentVersion = current;
            LoadContents();
        }

        private void OnGUI()
        {
            LoadContents();

            GUILayout.BeginVertical();

            GUILayout.Space(10);
            GUI.Box(new Rect(13, 8, guiLogo.image.width, guiLogo.image.height), guiLogo, GUIStyle.none);

            GUILayout.Space(15);
            EditorGUILayout.BeginHorizontal();

            GUILayout.Space(120);
            EditorGUILayout.BeginVertical();

            EditorGUILayout.LabelField(guiNewUpdate, "WordWrappedLabel", GUILayout.Width(300));

            if (hasReleaseNotesUrl)
            {
                if (GUILayout.Button(guiBrowseReleaseNotes, Styles.HyperlinkStyle))
                {
                    Help.BrowseURL(package.Package.ReleaseNotesUrl);
                }
            }

            if (hasMessage || hasReleaseNotes)
            {
                GUILayout.Space(20);

                if (hasMessage)
                {
                    scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Width(405), GUILayout.Height(200));
                    EditorGUILayout.LabelField(guiPackageMessage, "WordWrappedLabel");
                    EditorGUILayout.EndScrollView();
                }

                if (hasReleaseNotes)
                {
                    scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Width(405), GUILayout.Height(200));
                    EditorGUILayout.LabelField(guiPackageReleaseNotes, "WordWrappedLabel");
                    EditorGUILayout.EndScrollView();
                }
            }

            GUILayout.Space(20);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(guiDownloadNewVersion, GUILayout.Width(200)))
            {
                Help.BrowseURL(package.Package.Url);
            }

            if (GUILayout.Button(guiSkipThisVersion, GUILayout.Width(200)))
            {
                var settings = EntryPoint.ApplicationManager.UserSettings;
                var skipVersions = settings.Get<TheVersion[]>(Constants.SkipVersionKey).Append(package.Package.Version);
                settings.Set<TheVersion[]>(Constants.SkipVersionKey, skipVersions);
                this.Close();
            }

            if (GUILayout.Button(guiRemindMeTomorrow, GUILayout.Width(200)))
            {
                var settings = EntryPoint.ApplicationManager.UserSettings;
                var tomorrow = DateTimeOffset.Now.AddDays(1).ToString(Constants.Iso8601Format);
                settings.Set<string>(Constants.UpdateReminderDateKey, tomorrow);
                this.Close();
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();

            GUILayout.Space(8);

            GUILayout.Space(10);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void LoadContents()
        {
            if (guiLogo != null)
                return;

            guiLogo = new GUIContent(Styles.BigLogo);
            guiNewUpdate = new GUIContent(String.Format(newUpdateMessage, currentVersion, package.Package.Version.ToString()));
            guiSkipThisVersion = new GUIContent(skipThisVersionMessage);
            guiRemindMeTomorrow = new GUIContent(remingMeTomorrowMessage);
            guiDownloadNewVersion = new GUIContent(downloadNewVersionMessage);
            guiBrowseReleaseNotes = new GUIContent(browseReleaseNotes);
            hasMessage = !String.IsNullOrEmpty(package.Package.Message);
            hasReleaseNotes = !String.IsNullOrEmpty(package.Package.ReleaseNotes);
            hasReleaseNotesUrl = !String.IsNullOrEmpty(package.Package.ReleaseNotesUrl);
            if (hasMessage)
                guiPackageMessage = new GUIContent(package.Package.Message);
            if (hasReleaseNotes)
                guiPackageReleaseNotes = new GUIContent(package.Package.ReleaseNotes);
        }

    }
}
