using LiveSplit.Model;
using LiveSplit.TimeFormatters;
using LiveSplit.UI.Components;
using LiveSplit.UI;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Xml;
using System.Windows.Forms;
using System.Diagnostics;

namespace LiveSplit.Syndicate
{
    class SyndicateComponent : LogicComponent
    {
        public override string ComponentName
        {
            get { return "Syndicate (2012)"; }
        }

        public SyndicateSettings Settings { get; set; }

        public bool Disposed { get; private set; }
        public bool IsLayoutComponent { get; private set; }

        private TimerModel _timer;
        private GameMemory _gameMemory;
        private LiveSplitState _state;

        public SyndicateComponent(LiveSplitState state, bool isLayoutComponent)
        {
            _state = state;
            this.IsLayoutComponent = isLayoutComponent;

            this.Settings = new SyndicateSettings();

           _timer = new TimerModel { CurrentState = state };

            _gameMemory = new GameMemory(this.Settings);
            _gameMemory.OnFirstLevelLoading += gameMemory_OnFirstLevelLoading;
            _gameMemory.OnPlayerGainedControl += gameMemory_OnPlayerGainedControl;
            _gameMemory.OnLoadStarted += gameMemory_OnLoadStarted;
            _gameMemory.OnLoadFinished += gameMemory_OnLoadFinished;
            _gameMemory.OnSplitCompleted += gameMemory_OnSplitCompleted;
            state.OnStart += State_OnStart;
            _gameMemory.StartMonitoring();
        }

        public override void Dispose()
        {
            this.Disposed = true;

            _state.OnStart -= State_OnStart;

            if (_gameMemory != null)
            {
                _gameMemory.Stop();
            }

        }

        void State_OnStart(object sender, EventArgs e)
        {
            _gameMemory.resetSplitStates();
        }

        void gameMemory_OnFirstLevelLoading(object sender, EventArgs e)
        {
            if (this.Settings.AutoReset)
            {
                _timer.Reset();
            }
        }

        void gameMemory_OnPlayerGainedControl(object sender, EventArgs e)
        {
            if (this.Settings.AutoStart)
            {
                _timer.Start();
            }
        }

        void gameMemory_OnLoadStarted(object sender, EventArgs e)
        {
            _state.IsGameTimePaused = true;
        }

        void gameMemory_OnLoadFinished(object sender, EventArgs e)
        {
            _state.IsGameTimePaused = false;
        }

        void gameMemory_OnSplitCompleted(object sender, GameMemory.SplitArea split, uint frame)
        {
            Debug.WriteLineIf(split != GameMemory.SplitArea.None, String.Format("[NoLoads] Trying to split {0}, State: {1} - {2}", split, _gameMemory.splitStates[(int)split], frame));
            if (_state.CurrentPhase == TimerPhase.Running && !_gameMemory.splitStates[(int)split] &&
                ((split == GameMemory.SplitArea.SC1_WakeUpCall && this.Settings.sC01) ||
                (split == GameMemory.SplitArea.SC1_Tutorial1 && this.Settings.sC01_T) ||
                (split == GameMemory.SplitArea.SC2_AssaultOnAspari && this.Settings.sC02) ||
                (split == GameMemory.SplitArea.SC2_Tutorial2 && this.Settings.sC02_T) ||
                (split == GameMemory.SplitArea.SC3_AspariExtraction && this.Settings.sC03) ||
                (split == GameMemory.SplitArea.SC4_A_train_to_catch && this.Settings.sC04) ||
                (split == GameMemory.SplitArea.SC5_Escape_from_Los_angeles && this.Settings.sC05) ||
                (split == GameMemory.SplitArea.SC6_Eurocorp && this.Settings.sC06) ||
                (split == GameMemory.SplitArea.SC6_Tutorial3 && this.Settings.sC06_T) ||
                (split == GameMemory.SplitArea.SC7_Voyeur_Central && this.Settings.sC07) ||
                (split == GameMemory.SplitArea.SC8_Cayman_Global && this.Settings.sC08) ||
                (split == GameMemory.SplitArea.SC9_The_Floating_City && this.Settings.sC09) ||
                (split == GameMemory.SplitArea.SC10_Behind_the_scenes && this.Settings.sC10) ||
                (split == GameMemory.SplitArea.SC11_Ramon && this.Settings.sC11) ||
                (split == GameMemory.SplitArea.SC12_Downzone && this.Settings.sC12) ||
                (split == GameMemory.SplitArea.SC13_Betrayed && this.Settings.sC13) ||
                (split == GameMemory.SplitArea.SC14_The_wall && this.Settings.sC14) ||
                (split == GameMemory.SplitArea.SC15_Kris && this.Settings.sC15) ||
                (split == GameMemory.SplitArea.SC16_HumanResources && this.Settings.sC16) ||
                (split == GameMemory.SplitArea.SC17_CorporateWar && this.Settings.sC17) ||
                (split == GameMemory.SplitArea.SC18_SpireAccess && this.Settings.sC18) ||
                (split == GameMemory.SplitArea.SC19_BurningTower && this.Settings.sC19) ||
                (split == GameMemory.SplitArea.SC20_Jack_Denham && this.Settings.sC20)))
            {
                Trace.WriteLine(String.Format("[NoLoads] {0} Split - {1}", split, frame));
                _timer.Split();
                _gameMemory.splitStates[(int)split] = true;
            }
        }

        public override XmlNode GetSettings(XmlDocument document)
        {
            return this.Settings.GetSettings(document);
        }

        public override Control GetSettingsControl(LayoutMode mode)
        {
            return this.Settings;
        }

        public override void SetSettings(XmlNode settings)
        {
            this.Settings.SetSettings(settings);
        }

        public override void Update(IInvalidator invalidator, LiveSplitState state, float width, float height, LayoutMode mode) { }
        //public override void RenameComparison(string oldName, string newName) { }
    }
}
