/* Copyright (c) 2024 Rick (rick 'at' gibbed 'dot' us)
 *
 * This software is provided 'as-is', without any express or implied
 * warranty. In no event will the authors be held liable for any damages
 * arising from the use of this software.
 *
 * Permission is granted to anyone to use this software for any purpose,
 * including commercial applications, and to alter it and redistribute it
 * freely, subject to the following restrictions:
 *
 * 1. The origin of this software must not be misrepresented; you must not
 *    claim that you wrote the original software. If you use this software
 *    in a product, an acknowledgment in the product documentation would
 *    be appreciated but is not required.
 *
 * 2. Altered source versions must be plainly marked as such, and must not
 *    be misrepresented as being the original software.
 *
 * 3. This notice may not be removed or altered from any source
 *    distribution.
 */

/*
 * Modificações e melhorias por: https://github.com/olucasmf (PT-BR)
 * Modifications and improvements by: https://github.com/olucasmf (EN)
 */

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Windows.Forms;
using static SAM.Game.InvariantShorthand;
using APITypes = SAM.API.Types;
using System.Threading.Tasks;
using System.Threading;
using SAM.Game.Stats;
using Timer = System.Windows.Forms.Timer;

namespace SAM.Game
{
    internal partial class Manager : Form
    {
        private readonly long _GameId;
        private readonly API.Client _SteamClient;

        private readonly WebClient _IconDownloader = new();

        private readonly List<Stats.AchievementInfo> _IconQueue = new();
        private readonly List<Stats.StatDefinition> _StatDefinitions = new();

        private readonly List<Stats.AchievementDefinition> _AchievementDefinitions = new();

        private readonly BindingList<Stats.StatInfo> _Statistics = new();

        private readonly API.Callbacks.UserStatsReceived _UserStatsReceivedCallback;

        private readonly ToolStripProgressBar _ProgressBar;
        private readonly ToolStripLabel _UnlockCountdownLabel;

        private readonly ToolStripLabel _AutoUnlockLabel;
        private readonly ToolStripControlHost _AutoUnlockTimeHost;
        private readonly NumericUpDown _AutoUnlockTimeSpinner;
        private readonly ToolStripButton _AutoUnlockButton;

        private bool _IsUpdatingAchievementList;

        private CancellationTokenSource _AutoUnlockCts;

        private bool _ShowLocked = true;
        private bool _ShowUnlocked = true;
        private string _Filter = "";

        private readonly List<Stats.AchievementInfo> _Achievements = new();

        public Manager(long gameId, API.Client client)
        {
            this.InitializeComponent();

            this._AchievementListView.Sorting = SortOrder.None;

            // Sempre abrir em 'locked'
            this._DisplayLockedOnlyButton.Checked = true;
            this._DisplayUnlockedOnlyButton.Checked = false;

            this._UnlockCountdownLabel = new ToolStripLabel("");
            this._ProgressBar = new ToolStripProgressBar
            {
                Name = "_ProgressBar",
                Size = new Size(150, 16),
                Style = ProgressBarStyle.Continuous,
            };
            int progressBarIndex = this._MainStatusStrip.Items.Count;
            this._MainStatusStrip.Items.Add(this._UnlockCountdownLabel);
            this._MainStatusStrip.Items.Add(this._ProgressBar);

            this._AutoUnlockLabel = new ToolStripLabel("Auto (minutes):");
            this._AutoUnlockTimeSpinner = new NumericUpDown { Width = 50, Minimum = 1, Maximum = 999, Value = 1 };
            this._AutoUnlockTimeHost = new ToolStripControlHost(this._AutoUnlockTimeSpinner);
            this._AutoUnlockButton = new ToolStripButton("Start Auto");
            this._AutoUnlockButton.Click += this.OnAutoUnlock;

            this._AchievementsToolStrip.Items.Add(new ToolStripSeparator());
            this._AchievementsToolStrip.Items.Add(this._AutoUnlockLabel);
            this._AchievementsToolStrip.Items.Add(this._AutoUnlockTimeHost);
            this._AchievementsToolStrip.Items.Add(this._AutoUnlockButton);

            this._AchievementListView.AllowDrop = true;
            this._AchievementListView.ItemDrag += this.OnAchievementItemDrag;
            this._AchievementListView.DragEnter += this.OnAchievementDragEnter;
            this._AchievementListView.DragDrop += this.OnAchievementDragDrop;

            this._MainTabControl.SelectedTab = this._AchievementsTabPage;
            //this.statisticsList.Enabled = this.checkBox1.Checked;

            this._AchievementImageList.Images.Add("Blank", new Bitmap(64, 64));

            this._StatisticsDataGridView.AutoGenerateColumns = false;

            this._StatisticsDataGridView.Columns.Add("name", "Name");
            this._StatisticsDataGridView.Columns[0].ReadOnly = true;
            this._StatisticsDataGridView.Columns[0].Width = 200;
            this._StatisticsDataGridView.Columns[0].DataPropertyName = "DisplayName";

            this._StatisticsDataGridView.Columns.Add("value", "Value");
            this._StatisticsDataGridView.Columns[1].ReadOnly = this._EnableStatsEditingCheckBox.Checked == false;
            this._StatisticsDataGridView.Columns[1].Width = 90;
            this._StatisticsDataGridView.Columns[1].DataPropertyName = "Value";

            this._StatisticsDataGridView.Columns.Add("extra", "Extra");
            this._StatisticsDataGridView.Columns[2].ReadOnly = true;
            this._StatisticsDataGridView.Columns[2].Width = 200;
            this._StatisticsDataGridView.Columns[2].DataPropertyName = "Extra";

            this._StatisticsDataGridView.DataSource = new BindingSource()
            {
                DataSource = this._Statistics,
            };

            this._GameId = gameId;
            this._SteamClient = client;

            this._IconDownloader.DownloadDataCompleted += this.OnIconDownload;

            base.Text = "AutoSAM.Game 7.0";
            string name = this._SteamClient.SteamApps001.GetAppData((uint)this._GameId, "name");
            if (name != null)
            {
                base.Text += " | " + name;
            }
            else
            {
                base.Text += " | " + this._GameId.ToString(CultureInfo.InvariantCulture);
            }

            this._UserStatsReceivedCallback = client.CreateAndRegisterCallback<API.Callbacks.UserStatsReceived>();
            this._UserStatsReceivedCallback.OnRun += this.OnUserStatsReceived;

            this.RefreshStats();
        }

        private void AddAchievementIcon(Stats.AchievementInfo info, Image icon)
        {
            if (icon == null)
            {
                info.ImageIndex = 0;
            }
            else
            {
                info.ImageIndex = this._AchievementImageList.Images.Count;
                this._AchievementImageList.Images.Add(info.IsAchieved == true ? info.IconNormal : info.IconLocked, icon);
            }
        }

        private void OnIconDownload(object sender, DownloadDataCompletedEventArgs e)
        {
            if (e.Error == null && e.Cancelled == false)
            {
                var info = (Stats.AchievementInfo)e.UserState;

                Bitmap bitmap;
                try
                {
                    using (MemoryStream stream = new())
                    {
                        stream.Write(e.Result, 0, e.Result.Length);
                        bitmap = new(stream);
                    }
                }
                catch (Exception)
                {
                    bitmap = null;
                }

                this.AddAchievementIcon(info, bitmap);
                this._AchievementListView.Update();
            }

            this.DownloadNextIcon();
        }

        private void DownloadNextIcon()
        {
            if (this._IconQueue.Count == 0)
            {
                this._DownloadStatusLabel.Visible = false;
                return;
            }

            if (this._IconDownloader.IsBusy == true)
            {
                return;
            }

            this._DownloadStatusLabel.Text = $"Downloading {this._IconQueue.Count} icons...";
            this._DownloadStatusLabel.Visible = true;

            var info = this._IconQueue[0];
            this._IconQueue.RemoveAt(0);


            this._IconDownloader.DownloadDataAsync(
                new Uri(_($"https://cdn.steamstatic.com/steamcommunity/public/images/apps/{this._GameId}/{(info.IsAchieved == true ? info.IconNormal : info.IconLocked)}")),
                info);
        }

        private static string TranslateError(int id) => id switch
        {
            2 => "generic error -- this usually means you don't own the game",
            _ => _($"{id}"),
        };

        private static string GetLocalizedString(KeyValue kv, string language, string defaultValue)
        {
            var name = kv[language].AsString("");
            if (string.IsNullOrEmpty(name) == false)
            {
                return name;
            }

            if (language != "english")
            {
                name = kv["english"].AsString("");
                if (string.IsNullOrEmpty(name) == false)
                {
                    return name;
                }
            }

            name = kv.AsString("");
            if (string.IsNullOrEmpty(name) == false)
            {
                return name;
            }

            return defaultValue;
        }

        private bool LoadUserGameStatsSchema()
        {
            string path;
            try
            {
                string fileName = _($"UserGameStatsSchema_{this._GameId}.bin");
                path = API.Steam.GetInstallPath();
                path = Path.Combine(path, "appcache", "stats", fileName);
                if (File.Exists(path) == false)
                {
                    return false;
                }
            }
            catch (Exception)
            {
                return false;
            }

            var kv = KeyValue.LoadAsBinary(path);
            if (kv == null)
            {
                return false;
            }

            var currentLanguage = this._SteamClient.SteamApps008.GetCurrentGameLanguage();

            this._AchievementDefinitions.Clear();
            this._StatDefinitions.Clear();

            var stats = kv[this._GameId.ToString(CultureInfo.InvariantCulture)]["stats"];
            if (stats.Valid == false || stats.Children == null)
            {
                return false;
            }

            foreach (var stat in stats.Children)
            {
                if (stat.Valid == false)
                {
                    continue;
                }

                var rawType = stat["type_int"].Valid
                                  ? stat["type_int"].AsInteger(0)
                                  : stat["type"].AsInteger(0);
                var type = (APITypes.UserStatType)rawType;
                switch (type)
                {
                    case APITypes.UserStatType.Invalid:
                    {
                        break;
                    }

                    case APITypes.UserStatType.Integer:
                    {
                        var id = stat["name"].AsString("");
                        string name = GetLocalizedString(stat["display"]["name"], currentLanguage, id);

                        this._StatDefinitions.Add(new Stats.IntegerStatDefinition()
                        {
                            Id = stat["name"].AsString(""),
                            DisplayName = name,
                            MinValue = stat["min"].AsInteger(int.MinValue),
                            MaxValue = stat["max"].AsInteger(int.MaxValue),
                            MaxChange = stat["maxchange"].AsInteger(0),
                            IncrementOnly = stat["incrementonly"].AsBoolean(false),
                            SetByTrustedGameServer = stat["bSetByTrustedGS"].AsBoolean(false),
                            DefaultValue = stat["default"].AsInteger(0),
                            Permission = stat["permission"].AsInteger(0),
                        });
                        break;
                    }

                    case APITypes.UserStatType.Float:
                    case APITypes.UserStatType.AverageRate:
                    {
                        var id = stat["name"].AsString("");
                        string name = GetLocalizedString(stat["display"]["name"], currentLanguage, id);

                        this._StatDefinitions.Add(new Stats.FloatStatDefinition()
                        {
                            Id = stat["name"].AsString(""),
                            DisplayName = name,
                            MinValue = stat["min"].AsFloat(float.MinValue),
                            MaxValue = stat["max"].AsFloat(float.MaxValue),
                            MaxChange = stat["maxchange"].AsFloat(0.0f),
                            IncrementOnly = stat["incrementonly"].AsBoolean(false),
                            DefaultValue = stat["default"].AsFloat(0.0f),
                            Permission = stat["permission"].AsInteger(0),
                        });
                        break;
                    }

                    case APITypes.UserStatType.Achievements:
                    case APITypes.UserStatType.GroupAchievements:
                    {
                        if (stat.Children != null)
                        {
                            foreach (var bits in stat.Children.Where(
                                b => string.Compare(b.Name, "bits", StringComparison.InvariantCultureIgnoreCase) == 0))
                            {
                                if (bits.Valid == false ||
                                    bits.Children == null)
                                {
                                    continue;
                                }

                                foreach (var bit in bits.Children)
                                {
                                    string id = bit["name"].AsString("");
                                    string name = GetLocalizedString(bit["display"]["name"], currentLanguage, id);
                                    string desc = GetLocalizedString(bit["display"]["desc"], currentLanguage, "");

                                    this._AchievementDefinitions.Add(new()
                                    {
                                        Id = id,
                                        Name = name,
                                        Description = desc,
                                        IconNormal = bit["display"]["icon"].AsString(""),
                                        IconLocked = bit["display"]["icon_gray"].AsString(""),
                                        IsHidden = bit["display"]["hidden"].AsBoolean(false),
                                        Permission = bit["permission"].AsInteger(0),
                                    });
                                }
                            }
                        }

                        break;
                    }

                    default:
                    {
                        throw new InvalidOperationException("invalid stat type");
                    }
                }
            }

            return true;
        }

        private void OnUserStatsReceived(APITypes.UserStatsReceived param)
        {
            if (param.Result != 1)
            {
                this._GameStatusLabel.Text = $"Error while retrieving stats: {TranslateError(param.Result)}";
                this.EnableInput();
                return;
            }

            if (this.LoadUserGameStatsSchema() == false)
            {
                this._GameStatusLabel.Text = "Failed to load schema.";
                this.EnableInput();
                return;
            }

            try
            {
                this.GetAchievements();
            }
            catch (Exception e)
            {
                this._GameStatusLabel.Text = "Error when handling achievements retrieval.";
                this.EnableInput();
                MessageBox.Show(
                    "Error when handling achievements retrieval:\n" + e,
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            try
            {
                this.GetStatistics();
            }
            catch (Exception e)
            {
                this._GameStatusLabel.Text = "Error when handling stats retrieval.";
                this.EnableInput();
                MessageBox.Show(
                    "Error when handling stats retrieval:\n" + e,
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            this._GameStatusLabel.Text = $"Retrieved {this._AchievementListView.Items.Count} achievements and {this._StatisticsDataGridView.Rows.Count} statistics.";
            this.EnableInput();
        }

        private void RefreshStats()
        {
            this._AchievementListView.Items.Clear();
            this._StatisticsDataGridView.Rows.Clear();

            var steamId = this._SteamClient.SteamUser.GetSteamId();

            // This still triggers the UserStatsReceived callback, in addition to the callresult.
            // No need to implement callresults for the time being.
            var callHandle = this._SteamClient.SteamUserStats.RequestUserStats(steamId);
            if (callHandle == API.CallHandle.Invalid)
            {
                MessageBox.Show(this, "Failed.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            this._GameStatusLabel.Text = "Retrieving stat information...";
            this.DisableInput();
        }

        private void GetAchievements()
        {
            var textSearch = this._MatchingStringTextBox.Text.Length > 0
                ? this._MatchingStringTextBox.Text
                : null;

            this._IsUpdatingAchievementList = true;

            this._AchievementListView.BeginUpdate();
            this._AchievementListView.Items.Clear();

            bool wantLocked = this._DisplayLockedOnlyButton.Checked == true;
            bool wantUnlocked = this._DisplayUnlockedOnlyButton.Checked == true;

            var achievements = new List<Stats.AchievementInfo>();
            foreach (var def in this._AchievementDefinitions)
            {
                if (string.IsNullOrEmpty(def.Id) == true)
                {
                    continue;
                }

                if (this._SteamClient.SteamUserStats.GetAchievementAndUnlockTime(
                    def.Id,
                    out bool isAchieved,
                    out uint unlockTime) == false)
                {
                    continue;
                }

                var info = new Stats.AchievementInfo
                {
                    Id = def.Id,
                    IsAchieved = isAchieved,
                    UnlockTime = isAchieved && unlockTime > 0
                        ? DateTimeOffset.FromUnixTimeSeconds(unlockTime).LocalDateTime
                        : null,
                    IconNormal = string.IsNullOrEmpty(def.IconNormal) ? null : def.IconNormal,
                    IconLocked = string.IsNullOrEmpty(def.IconLocked) ? def.IconNormal : def.IconLocked,
                    Permission = def.Permission,
                    Name = def.Name,
                    Description = def.Description,
                };
                achievements.Add(info);
            }

            if (achievements.Count > 0)
            {
                this._ProgressBar.Maximum = achievements.Count;
                this._ProgressBar.Value = achievements.Count(a => a.IsAchieved);
                this._ProgressBar.Visible = true;
            }
            else
            {
                this._ProgressBar.Visible = false;
            }

            var query = achievements.AsEnumerable();

            if (wantLocked)
            {
                query = query.Where(a => a.IsAchieved == false);
            }
            else if (wantUnlocked)
            {
                query = query.Where(a => a.IsAchieved == true);
            }

            if (textSearch != null)
            {
                query = query.Where(
                    a => a.Name.IndexOf(textSearch, StringComparison.OrdinalIgnoreCase) >= 0 ||
                         a.Description.IndexOf(textSearch, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            try
            {
                this._AchievementListView.Items.AddRange(query.Select(info =>
                {
                    var item = new ListViewItem
                    {
                        Checked = info.IsAchieved,
                    Tag = info,
                    Text = info.Name,
                        BackColor = (info.Permission & 3) == 0 ? Color.Black : Color.FromArgb(64, 0, 0),
                };

                info.Item = item;

                if (item.Text.StartsWith("#", StringComparison.InvariantCulture) == true)
                {
                    item.Text = info.Id;
                    item.SubItems.Add("");
                }
                else
                {
                    item.SubItems.Add(info.Description);
                }

                    item.SubItems.Add(info.UnlockTime.HasValue ? info.UnlockTime.Value.ToString() : "");

                info.ImageIndex = 0;

                this.AddAchievementToIconQueue(info, false);
                    return item;
                }).ToArray());
            }
            finally
            {
            this._AchievementListView.EndUpdate();
                this._AchievementListView.Sorting = SortOrder.None;
            }

            this.DownloadNextIcon();
        }

        private void GetStatistics()
        {
            this._Statistics.Clear();
            foreach (var stat in this._StatDefinitions)
            {
                if (string.IsNullOrEmpty(stat.Id) == true)
                {
                    continue;
                }

                if (stat is Stats.IntegerStatDefinition intStat)
                {
                    if (this._SteamClient.SteamUserStats.GetStatValue(intStat.Id, out int value) == false)
                    {
                        continue;
                    }
                    this._Statistics.Add(new Stats.IntStatInfo()
                    {
                        Id = intStat.Id,
                        DisplayName = intStat.DisplayName,
                        IntValue = value,
                        OriginalValue = value,
                        IsIncrementOnly = intStat.IncrementOnly,
                        Permission = intStat.Permission,
                    });
                }
                else if (stat is Stats.FloatStatDefinition floatStat)
                {
                    if (this._SteamClient.SteamUserStats.GetStatValue(floatStat.Id, out float value) == false)
                    {
                        continue;
                    }
                    this._Statistics.Add(new Stats.FloatStatInfo()
                    {
                        Id = floatStat.Id,
                        DisplayName = floatStat.DisplayName,
                        FloatValue = value,
                        OriginalValue = value,
                        IsIncrementOnly = floatStat.IncrementOnly,
                        Permission = floatStat.Permission,
                    });
                }
            }
        }

        private void AddAchievementToIconQueue(Stats.AchievementInfo info, bool startDownload)
        {
            int imageIndex = this._AchievementImageList.Images.IndexOfKey(
                info.IsAchieved == true ? info.IconNormal : info.IconLocked);

            if (imageIndex >= 0)
            {
                info.ImageIndex = imageIndex;
            }
            else
            {
                this._IconQueue.Add(info);

                if (startDownload == true)
                {
                    this.DownloadNextIcon();
                }
            }
        }

        private int StoreAchievements()
        {
            if (this._AchievementListView.Items.Count == 0)
            {
                return 0;
            }

            List<Stats.AchievementInfo> achievements = new();
            foreach (ListViewItem item in this._AchievementListView.Items)
            {
                if (item.Tag is not Stats.AchievementInfo achievementInfo ||
                    achievementInfo.IsAchieved == item.Checked)
                {
                    continue;
                }

                achievementInfo.IsAchieved = item.Checked;
                achievements.Add(achievementInfo);
            }

            if (achievements.Count == 0)
            {
                return 0;
            }

            foreach (var info in achievements)
            {
                if (this._SteamClient.SteamUserStats.SetAchievement(info.Id, info.IsAchieved) == false)
                {
                    MessageBox.Show(
                        this,
                        $"An error occurred while setting the state for {info.Id}, aborting store.",
                        "Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return -1;
                }
            }

            return achievements.Count;
        }

        private int StoreStatistics()
        {
            if (this._Statistics.Count == 0)
            {
                return 0;
            }

            var statistics = this._Statistics.Where(stat => stat.IsModified == true).ToList();
            if (statistics.Count == 0)
            {
                return 0;
            }

            foreach (var stat in statistics)
            {
                if (stat is Stats.IntStatInfo intStat)
                {
                    if (this._SteamClient.SteamUserStats.SetStatValue(
                        intStat.Id,
                        intStat.IntValue) == false)
                    {
                        MessageBox.Show(
                            this,
                            $"An error occurred while setting the value for {stat.Id}, aborting store.",
                            "Error",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                        return -1;
                    }
                }
                else if (stat is Stats.FloatStatInfo floatStat)
                {
                    if (this._SteamClient.SteamUserStats.SetStatValue(
                        floatStat.Id,
                        floatStat.FloatValue) == false)
                    {
                        MessageBox.Show(
                            this,
                            $"An error occurred while setting the value for {stat.Id}, aborting store.",
                            "Error",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                        return -1;
                    }
                }
                else
                {
                    throw new InvalidOperationException("unsupported stat type");
                }
            }

            return statistics.Count;
        }

        private void DisableInput()
        {
            this._ReloadButton.Enabled = false;
            this._StoreButton.Enabled = false;
        }

        private void EnableInput()
        {
            this._ReloadButton.Enabled = true;
            this._StoreButton.Enabled = true;
        }

        private void OnTimer(object sender, EventArgs e)
        {
            this._CallbackTimer.Enabled = false;
            this._SteamClient.RunCallbacks(false);
            this._CallbackTimer.Enabled = true;
        }

        private void OnRefresh(object sender, EventArgs e)
        {
            this.RefreshStats();
        }

        private void OnLockAll(object sender, EventArgs e)
        {
            foreach (ListViewItem item in this._AchievementListView.Items)
            {
                item.Checked = false;
            }
            this.ForceGlobalAchievementsRefresh();
        }

        private void OnInvertAll(object sender, EventArgs e)
        {
            foreach (ListViewItem item in this._AchievementListView.Items)
            {
                item.Checked = !item.Checked;
            }
            this.ForceGlobalAchievementsRefresh();
        }

        private void OnUnlockAll(object sender, EventArgs e)
        {
            foreach (ListViewItem item in this._AchievementListView.Items)
            {
                item.Checked = true;
            }
            this.ForceGlobalAchievementsRefresh();
        }

        private bool Store()
        {
            if (this._SteamClient.SteamUserStats.StoreStats() == false)
            {
                MessageBox.Show(
                    this,
                    "An error occurred while storing, aborting.",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return false;
            }

            return true;
        }

        private void OnStore(object sender, EventArgs e)
        {
            int achievements = this.StoreAchievements();
            if (achievements < 0)
            {
                this.RefreshStats();
                return;
            }

            int stats = this.StoreStatistics();
            if (stats < 0)
            {
                this.RefreshStats();
                return;
            }

            if (this.Store() == false)
            {
                this.RefreshStats();
                return;
            }

            MessageBox.Show(
                this,
                $"Stored {achievements} achievements and {stats} statistics.",
                "Information",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            this.RefreshStats();
        }

        private void OnStatDataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            if (e.Context != DataGridViewDataErrorContexts.Commit)
            {
                return;
            }

            var view = (DataGridView)sender;
            if (e.Exception is Stats.StatIsProtectedException)
            {
                e.ThrowException = false;
                e.Cancel = true;
                view.Rows[e.RowIndex].ErrorText = "Stat is protected! -- you can't modify it";
            }
            else
            {
                e.ThrowException = false;
                e.Cancel = true;
                view.Rows[e.RowIndex].ErrorText = "Invalid value";
            }
        }

        private void OnStatAgreementChecked(object sender, EventArgs e)
        {
            this._StatisticsDataGridView.Columns[1].ReadOnly = this._EnableStatsEditingCheckBox.Checked == false;
        }

        private void OnStatCellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            var view = (DataGridView)sender;
            view.Rows[e.RowIndex].ErrorText = "";
        }

        private void OnResetAllStats(object sender, EventArgs e)
        {
            if (MessageBox.Show(
                "Are you absolutely sure you want to reset stats?",
                "Warning",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) == DialogResult.No)
            {
                return;
            }

            bool achievementsToo = DialogResult.Yes == MessageBox.Show(
                "Do you want to reset achievements too?",
                "Question",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (MessageBox.Show(
                "Really really sure?",
                "Warning",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Error) == DialogResult.No)
            {
                return;
            }

            if (this._SteamClient.SteamUserStats.ResetAllStats(achievementsToo) == false)
            {
                MessageBox.Show(this, "Failed.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            this.RefreshStats();
        }

        private void OnCheckAchievement(object sender, ItemCheckEventArgs e)
        {
            if (this._IsUpdatingAchievementList)
            {
                return;
            }

            if (sender != this._AchievementListView)
            {
                return;
            }

            if (this._AchievementListView.Items[e.Index].Tag is not Stats.AchievementInfo info)
            {
                return;
            }

            if ((info.Permission & 3) != 0)
            {
                MessageBox.Show(
                    this,
                    "Sorry, but this is a protected achievement and cannot be managed with AutoSAM.",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                e.NewValue = e.CurrentValue;
            }
        }

        private void OnDisplayUncheckedOnly(object sender, EventArgs e)
        {
            if ((sender as ToolStripButton).Checked == true)
            {
                this._DisplayLockedOnlyButton.Checked = false;
            }

            this.GetAchievements();
        }

        private void OnDisplayCheckedOnly(object sender, EventArgs e)
        {
            if ((sender as ToolStripButton).Checked == true)
            {
                this._DisplayUnlockedOnlyButton.Checked = false;
            }

            this.GetAchievements();
        }

        private void OnFilterUpdate(object sender, System.EventArgs e)
        {
            this.UpdateAchievements(this._DisplayLockedOnlyButton.Checked, this._DisplayUnlockedOnlyButton.Checked, this._MatchingStringTextBox.Text);
        }

        private void ToggleControls(bool enabled)
        {
            this._StoreButton.Enabled = enabled;
            this._ReloadButton.Enabled = enabled;
            this._ResetButton.Enabled = enabled;
            this._LockAllButton.Enabled = enabled;
            this._InvertAllButton.Enabled = enabled;
            this._UnlockAllButton.Enabled = enabled;
            this._AutoUnlockTimeHost.Enabled = enabled;
            this._DisplayLockedOnlyButton.Enabled = enabled;
            this._DisplayUnlockedOnlyButton.Enabled = enabled;
            this._MatchingStringTextBox.Enabled = enabled;
        }

        private async void OnAutoUnlock(object sender, EventArgs e)
        {
            this._AutoUnlockButton.Text = "Stop Auto";
            this._AutoUnlockButton.Click -= this.OnAutoUnlock;
            this._AutoUnlockButton.Click += this.OnStopAutoUnlock;
            this._AutoUnlockTimeSpinner.Enabled = false;

            // Bloquear interações do usuário
            this._AchievementListView.AllowDrop = false;
            this._AchievementListView.ItemDrag -= this.OnAchievementItemDrag;
            this._AchievementListView.DragEnter -= this.OnAchievementDragEnter;
            this._AchievementListView.DragDrop -= this.OnAchievementDragDrop;

            this._AutoUnlockCts = new CancellationTokenSource();
            var token = this._AutoUnlockCts.Token;

            try
            {
                int delay = (int)this._AutoUnlockTimeSpinner.Value * 60 * 1000;
                if (delay <= 0)
                {
                    MessageBox.Show("Auto time must be greater than zero.", "Invalid Time", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var itemsToUnlock = this._AchievementListView.Items.Cast<ListViewItem>()
                    .Where(item => !((AchievementInfo)item.Tag).IsAchieved)
                    .ToList();

                foreach (var item in itemsToUnlock)
                {
                    if (token.IsCancellationRequested)
                        break;

                    var info = (AchievementInfo)item.Tag;

                    // Timer visual
                    int seconds = delay / 1000;
                    for (int s = seconds; s > 0; s--)
                    {
                        int h = s / 3600;
                        int m = (s % 3600) / 60;
                        int sec = s % 60;
                        SafeUIAction(() => this._UnlockCountdownLabel.Text = string.Format("{0:D2}:{1:D2}:{2:D2}", h, m, sec));
                        await Task.Delay(1000, token);
                    }
                    SafeUIAction(() => this._UnlockCountdownLabel.Text = "00:00:00");

                    try
                    {
                        if (this._SteamClient.SteamUserStats.SetAchievement(info.Id, true) &&
                            this._SteamClient.SteamUserStats.StoreStats())
                        {
                            // Consultar o estado real da conquista na API
                            bool isAchieved;
                            uint unlockTime;
                            if (this._SteamClient.SteamUserStats.GetAchievementAndUnlockTime(info.Id, out isAchieved, out unlockTime))
                            {
                                info.IsAchieved = isAchieved;
                                info.UnlockTime = isAchieved && unlockTime > 0
                                    ? DateTimeOffset.FromUnixTimeSeconds(unlockTime).LocalDateTime
                                    : null;

                                // Atualizar a lista centralizada
                                var central = this._Achievements.FirstOrDefault(a => a.Id == info.Id);
                                if (central != null)
                                {
                                    central.IsAchieved = info.IsAchieved;
                                    central.UnlockTime = info.UnlockTime;
                                }
                            }
                            else
                            {
                                // fallback: marcar como achieved
                                info.IsAchieved = true;
                            }

                            if (this._ProgressBar.Value < this._ProgressBar.Maximum)
                                this._ProgressBar.Value++;

                            // Atualização sutil: remove só o item desbloqueado ou marca como checked
                            if (this._DisplayLockedOnlyButton.Checked && !this._DisplayUnlockedOnlyButton.Checked)
                            {
                                SafeUIAction(() => {
                                    this._AchievementListView.Items.Remove(item);
                                    if (this._AchievementListView.Items.Count == 0)
                                    {
                                        var emptyItem = new ListViewItem("Nenhuma conquista restante") { ForeColor = Color.Gray };
                                        this._AchievementListView.Items.Add(emptyItem);
                                    }
                                });
                            }
                            else
                            {
                                SafeUIAction(() => {
                                    item.Checked = true;
                                    item.EnsureVisible();
                                });
                            }
                        }
                        else
                        {
                            MessageBox.Show($"Failed to unlock achievement: {info.Name}.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Erro ao desbloquear conquista: {info.Name}\n{ex.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        break;
                    }
                }

                if (!token.IsCancellationRequested)
                {
                    MessageBox.Show("All achievements have been unlocked.", "Auto-Unlock Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (OperationCanceledException)
            {
                // Cancelado pelo usuário
            }
            finally
            {
                this._AutoUnlockButton.Text = "Start Auto";
                this._AutoUnlockButton.Click -= this.OnStopAutoUnlock;
                this._AutoUnlockButton.Click += this.OnAutoUnlock;
                this._AutoUnlockTimeSpinner.Enabled = true;
                SafeUIAction(() => {
                    this._AchievementListView.AllowDrop = true;
                    this._AchievementListView.ItemDrag += this.OnAchievementItemDrag;
                    this._AchievementListView.DragEnter += this.OnAchievementDragEnter;
                    this._AchievementListView.DragDrop += this.OnAchievementDragDrop;
                });
                SafeUIAction(() => this._UnlockCountdownLabel.Text = "");

                if (this._AutoUnlockCts != null)
                {
                    this._AutoUnlockCts.Dispose();
                    this._AutoUnlockCts = null;
                }
                this.ForceGlobalAchievementsRefresh();
            }
        }

        private void OnStopAutoUnlock(object sender, EventArgs e)
        {
            if (this._AutoUnlockCts != null)
            {
                this._AutoUnlockCts.Cancel();
            }
        }

        private void OnAchievementItemDrag(object sender, ItemDragEventArgs e)
        {
            this._AchievementListView.DoDragDrop(e.Item, DragDropEffects.Move);
        }

        private void OnAchievementDragEnter(object sender, DragEventArgs e)
        {
            e.Effect = DragDropEffects.Move;
        }

        private void OnAchievementDragDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetData(typeof(ListViewItem)) is ListViewItem draggedItem)
            {
                var dropPoint = this._AchievementListView.PointToClient(new Point(e.X, e.Y));
                var dropItem = this._AchievementListView.GetItemAt(dropPoint.X, dropPoint.Y);

                if (dropItem != null)
                {
                    var dropIndex = dropItem.Index;
                    this._AchievementListView.Items.Remove(draggedItem);
                    this._AchievementListView.Items.Insert(dropIndex, draggedItem);
                }
            }
        }

        private void UpdateAchievements(bool showLocked, bool showUnlocked, string filter)
        {
            this._ShowLocked = showLocked;
            this._ShowUnlocked = showUnlocked;
            this._Filter = filter;

            this.GetAchievements();
        }

        // Garante que ações de UI ocorram na thread principal
        private void SafeUIAction(Action a)
        {
            if (this.InvokeRequired)
                this.Invoke(a);
            else
                a();
        }

        private void Manager_Load(object sender, EventArgs e)
        {

        }

        private void OnTabChanged(object sender, EventArgs e)
        {
            if (this._MainTabControl.SelectedTab == this._AchievementsTabPage)
            {
                this.ForceGlobalAchievementsRefresh();
            }
        }

        /// <summary>
        /// Força atualização global das conquistas a partir do backend Steam e atualiza a interface.
        /// </summary>
        private void ForceGlobalAchievementsRefresh()
        {
            SafeUIAction(() =>
            {
                // Recarrega o estado das conquistas do Steam
                this._Achievements.Clear();
                foreach (var def in this._AchievementDefinitions)
                {
                    if (string.IsNullOrEmpty(def.Id))
                        continue;
                    if (this._SteamClient.SteamUserStats.GetAchievementAndUnlockTime(
                        def.Id,
                        out bool isAchieved,
                        out uint unlockTime) == false)
                        continue;
                    var info = new Stats.AchievementInfo
                    {
                        Id = def.Id,
                        IsAchieved = isAchieved,
                        UnlockTime = isAchieved && unlockTime > 0
                            ? DateTimeOffset.FromUnixTimeSeconds(unlockTime).LocalDateTime
                            : null,
                        IconNormal = string.IsNullOrEmpty(def.IconNormal) ? null : def.IconNormal,
                        IconLocked = string.IsNullOrEmpty(def.IconLocked) ? def.IconNormal : def.IconLocked,
                        Permission = def.Permission,
                        Name = def.Name,
                        Description = def.Description,
                    };
                    this._Achievements.Add(info);
                }
                // Atualiza a interface
                this.UpdateAchievements(
                    this._DisplayLockedOnlyButton.Checked,
                    this._DisplayUnlockedOnlyButton.Checked,
                    this._MatchingStringTextBox.Text);
            });
        }
    }
}
