using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using LiveSplit.ComponentUtil;

namespace LiveSplit.Syndicate
{
    class GameMemory
    {
        public enum SplitArea : int
        {
            None,
            SC1_WakeUpCall,
            SC1_Tutorial1,
            SC2_AssaultOnAspari,
            SC2_Tutorial2,
            SC3_AspariExtraction,
            SC4_A_train_to_catch,
            SC5_Escape_from_Los_angeles,
            SC6_Eurocorp,
            SC6_Tutorial3,
            SC7_Voyeur_Central,
            SC8_Cayman_Global,
            SC9_The_Floating_City,
            SC10_Behind_the_scenes,
            SC11_Ramon,
            SC12_Downzone,
            SC13_Betrayed,
            SC14_The_wall,
            SC15_Kris,
            SC16_HumanResources,
            SC17_CorporateWar,
            SC18_SpireAccess,
            SC19_BurningTower,
            SC20_Jack_Denham
        }

        public event EventHandler OnFirstLevelLoading;
        public event EventHandler OnPlayerGainedControl;
        public event EventHandler OnLoadStarted;
        public event EventHandler OnLoadFinished;
        public delegate void SplitCompletedEventHandler(object sender, SplitArea type, uint frame);
        public event SplitCompletedEventHandler OnSplitCompleted;

        private Task _thread;
        private CancellationTokenSource _cancelSource;
        private SynchronizationContext _uiThread;
        private List<int> _ignorePIDs;
        private SyndicateSettings _settings;

        private DeepPointer _isLoadingPtr;
        private DeepPointer _levelNamePtr;

        private static class LevelName
        {
            public const string C1_WakeUpCall = "S01_WAKEUPCALL";
            public const string C1_Tutorial1 = "S00_TUTORIAL1";
            public const string C2_AssaultOnAspari = "S03_EXECSEARCH";
            public const string C2_Tutorial2 = "S00_TUTORIAL2";
            public const string C3_AspariExtraction = "S04_EXECSEARCH_A";
            public const string C4_A_train_to_catch = "S04_EXECSEARCH_B";
            public const string C5_Escape_from_Los_angeles = "S04_EXECSEARCH_C";
            public const string C6_Eurocorp = "S05_LILYDRAWL";
            public const string C6_Tutorial3 = "S00_TUTORIAL3";
            public const string C7_Voyeur_Central = "S06_SURVEYLILY01";
            public const string C8_Cayman_Global = "S06_SURVEYLILY02";
            public const string C9_The_Floating_City = "S07_LABALLENA_INTRO";
            public const string C10_Behind_the_scenes = "S07_LABALLENA";
            public const string C11_Ramon = "S08_LABALLENA";
            public const string C12_Downzone = "S09_DOWNZONE01";
            public const string C13_Betrayed = "S09_DOWNZONE02";
            public const string C14_The_wall = "S11_DOWNZONE01";
            public const string C15_Kris = "S11_DOWNZONE02";
            public const string C16_HumanResources = "S13_HUMANRESOURCES";
            public const string C17_CorporateWar = "S15_RISINGFORCE01";
            public const string C18_SpireAccess = "S15_RISINGFORCE02";
            public const string C19_BurningTower = "S16_RISINGFORCE";
            public const string C20_Jack_Denham = "S16_DATACORE";            
        }

        public bool[] splitStates { get; set; }

        public void resetSplitStates()
        {
            for (int i = 0; i <= (int)SplitArea.SC20_Jack_Denham; i++)
            {
                splitStates[i] = false;
            }

        }

        public GameMemory(SyndicateSettings componentSettings)
        {
            _settings = componentSettings;
            splitStates = new bool[(int)SplitArea.SC20_Jack_Denham + 1];

            resetSplitStates();

            _isLoadingPtr = new DeepPointer(0x16ABB48); // == 1 if a loadscreen is happening
            _levelNamePtr = new DeepPointer("GameClasses_Win32_x86_Release.dll", 0x00AB67A8, 0x40, 0x270, 0x198);

            _ignorePIDs = new List<int>();
        }

        public void StartMonitoring()
        {
            if (_thread != null && _thread.Status == TaskStatus.Running)
            {
                throw new InvalidOperationException();
            }
            if (!(SynchronizationContext.Current is WindowsFormsSynchronizationContext))
            {
                throw new InvalidOperationException("SynchronizationContext.Current is not a UI thread.");
            }

            _uiThread = SynchronizationContext.Current;
            _cancelSource = new CancellationTokenSource();
            _thread = Task.Factory.StartNew(MemoryReadThread);
        }

        public void Stop()
        {
            if (_cancelSource == null || _thread == null || _thread.Status != TaskStatus.Running)
            {
                return;
            }

            _cancelSource.Cancel();
            _thread.Wait();
        }

        void MemoryReadThread()
        {
            Debug.WriteLine("[NoLoads] MemoryReadThread");

            while (!_cancelSource.IsCancellationRequested)
            {
                try
                {
                    Debug.WriteLine("[NoLoads] Waiting for syndicate.exe...");

                    Process game;
                    while ((game = GetGameProcess()) == null)
                    {
                        Thread.Sleep(250);
                        if (_cancelSource.IsCancellationRequested)
                        {
                            return;
                        }
                    }

                    Debug.WriteLine("[NoLoads] Got games process!");

                    uint frameCounter = 0;

                    bool prevIsLoading = false;
                    string prevStreamGroupId = String.Empty;

                    bool loadingStarted = false;

                    while (!game.HasExited)
                    {
                        string streamGroupId = String.Empty;
                        _levelNamePtr.DerefString(game, ReadStringType.UTF8, 25, out streamGroupId);
                        if(streamGroupId != null)
                        {
                            streamGroupId = streamGroupId.ToUpper();
                            if (streamGroupId != prevStreamGroupId)
                            {
                                //For safety... it may fail to split, but at least it won't kill load remover
                                if (prevStreamGroupId == LevelName.C1_WakeUpCall && streamGroupId == LevelName.C1_Tutorial1)
                                {
                                    Split(SplitArea.SC1_WakeUpCall, frameCounter);
                                }
                                else if (prevStreamGroupId == LevelName.C1_Tutorial1 && streamGroupId == LevelName.C2_AssaultOnAspari)
                                {
                                    Split(SplitArea.SC1_Tutorial1, frameCounter);
                                }
                                else if (prevStreamGroupId == LevelName.C2_AssaultOnAspari && streamGroupId == LevelName.C2_Tutorial2)
                                {
                                    Split(SplitArea.SC2_AssaultOnAspari, frameCounter);
                                }
                                else if (prevStreamGroupId == LevelName.C2_Tutorial2 && streamGroupId == LevelName.C3_AspariExtraction)
                                {
                                    Split(SplitArea.SC2_Tutorial2, frameCounter);
                                }
                                else if (prevStreamGroupId == LevelName.C3_AspariExtraction && streamGroupId == LevelName.C4_A_train_to_catch)
                                {
                                    Split(SplitArea.SC3_AspariExtraction, frameCounter);
                                }
                                else if (prevStreamGroupId == LevelName.C4_A_train_to_catch && streamGroupId == LevelName.C5_Escape_from_Los_angeles)
                                {
                                    Split(SplitArea.SC4_A_train_to_catch, frameCounter);
                                }
                                else if (prevStreamGroupId == LevelName.C5_Escape_from_Los_angeles && streamGroupId == LevelName.C6_Eurocorp)
                                {
                                    Split(SplitArea.SC5_Escape_from_Los_angeles, frameCounter);
                                }
                                else if (prevStreamGroupId == LevelName.C6_Eurocorp && streamGroupId == LevelName.C6_Tutorial3)
                                {
                                    Split(SplitArea.SC6_Eurocorp, frameCounter);
                                }
                                else if (prevStreamGroupId == LevelName.C6_Tutorial3 && streamGroupId == LevelName.C7_Voyeur_Central)
                                {
                                    Split(SplitArea.SC6_Tutorial3, frameCounter);
                                }
                                else if (prevStreamGroupId == LevelName.C7_Voyeur_Central && streamGroupId == LevelName.C8_Cayman_Global)
                                {
                                    Split(SplitArea.SC7_Voyeur_Central, frameCounter);
                                }
                                else if (prevStreamGroupId == LevelName.C8_Cayman_Global && streamGroupId == LevelName.C9_The_Floating_City)
                                {
                                    Split(SplitArea.SC8_Cayman_Global, frameCounter);
                                }
                                else if (prevStreamGroupId == LevelName.C9_The_Floating_City && streamGroupId == LevelName.C10_Behind_the_scenes)
                                {
                                    Split(SplitArea.SC9_The_Floating_City, frameCounter);
                                }
                                else if (prevStreamGroupId == LevelName.C10_Behind_the_scenes && streamGroupId == LevelName.C11_Ramon)
                                {
                                    Split(SplitArea.SC10_Behind_the_scenes, frameCounter);
                                }
                                else if (prevStreamGroupId == LevelName.C11_Ramon && streamGroupId == LevelName.C12_Downzone)
                                {
                                    Split(SplitArea.SC11_Ramon, frameCounter);
                                }
                                else if (prevStreamGroupId == LevelName.C12_Downzone && streamGroupId == LevelName.C13_Betrayed)
                                {
                                    Split(SplitArea.SC12_Downzone, frameCounter);
                                }
                                else if (prevStreamGroupId == LevelName.C13_Betrayed && streamGroupId == LevelName.C14_The_wall)
                                {
                                    Split(SplitArea.SC13_Betrayed, frameCounter);
                                }
                                else if (prevStreamGroupId == LevelName.C14_The_wall && streamGroupId == LevelName.C15_Kris)
                                {
                                    Split(SplitArea.SC14_The_wall, frameCounter);
                                }
                                else if (prevStreamGroupId == LevelName.C15_Kris && streamGroupId == LevelName.C16_HumanResources)
                                {
                                    Split(SplitArea.SC15_Kris, frameCounter);
                                }
                                else if (prevStreamGroupId == LevelName.C16_HumanResources && streamGroupId == LevelName.C17_CorporateWar)
                                {
                                    Split(SplitArea.SC16_HumanResources, frameCounter);
                                }
                                else if (prevStreamGroupId == LevelName.C17_CorporateWar && streamGroupId == LevelName.C18_SpireAccess)
                                {
                                    Split(SplitArea.SC17_CorporateWar, frameCounter);
                                }
                                else if (prevStreamGroupId == LevelName.C18_SpireAccess && streamGroupId == LevelName.C19_BurningTower)
                                {
                                    Split(SplitArea.SC18_SpireAccess, frameCounter);
                                }
                                else if (prevStreamGroupId == LevelName.C19_BurningTower && streamGroupId == LevelName.C20_Jack_Denham)
                                {
                                    Split(SplitArea.SC19_BurningTower, frameCounter);
                                }/*
                            else if (prevStreamGroupId == LevelName.C20_Jack_Denham)
                            {
                                Split(SplitArea.SC20_Jack_Denham, frameCounter);
                            }*/

                            }
                        }



                        bool isLoading;
                        _isLoadingPtr.Deref(game, out isLoading);
                        
                        

                        if (isLoading != prevIsLoading)
                        {
                            if (isLoading)
                            {
                                Debug.WriteLine(String.Format("[NoLoads] Load Start - {0}", frameCounter));

                                loadingStarted = true;

                                // pause game timer
                                _uiThread.Post(d =>
                                {
                                    if (this.OnLoadStarted != null)
                                    {
                                        this.OnLoadStarted(this, EventArgs.Empty);
                                    }
                                }, null);
                                /*
                                if (streamGroupId == LevelName.C1_WakeUpCall)
                                {
                                    // reset game timer
                                    _uiThread.Post(d =>
                                    {
                                        if (this.OnFirstLevelLoading != null)
                                        {
                                            this.OnFirstLevelLoading(this, EventArgs.Empty);
                                        }
                                    }, null);
                                }*/
                            }
                            else
                            {
                                Debug.WriteLine(String.Format("[NoLoads] Load End - {0}", frameCounter));

                                if (loadingStarted)
                                {
                                    loadingStarted = false;

                                    // unpause game timer
                                    _uiThread.Post(d =>
                                    {
                                        if (this.OnLoadFinished != null)
                                        {
                                            this.OnLoadFinished(this, EventArgs.Empty);
                                        }
                                    }, null);

                                    if (streamGroupId == LevelName.C1_WakeUpCall)
                                    {
                                        // start game timer
                                        _uiThread.Post(d =>
                                        {
                                            if (this.OnPlayerGainedControl != null)
                                            {
                                                this.OnPlayerGainedControl(this, EventArgs.Empty);
                                            }
                                        }, null);
                                    }
                                }
                            }
                        }

                        Debug.WriteLineIf(streamGroupId != prevStreamGroupId, String.Format("[NoLoads] streamGroupId changed from {0} to {1} - {2}", prevStreamGroupId, streamGroupId, frameCounter));
                        prevStreamGroupId = streamGroupId;
                        prevIsLoading = isLoading;
                        frameCounter++;

                        Thread.Sleep(15);

                        if (_cancelSource.IsCancellationRequested)
                        {
                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.ToString());
                    Thread.Sleep(1000);
                }
            }
        }

        private void Split(SplitArea split, uint frame)
        {
            Debug.WriteLine(String.Format("[NoLoads] split {0} - {1}", split, frame));
            _uiThread.Post(d =>
            {
                if (this.OnSplitCompleted != null)
                {
                    this.OnSplitCompleted(this, split, frame);
                }
            }, null);
        }

        Process GetGameProcess()
        {
            Process game = Process.GetProcesses().FirstOrDefault(p => p.ProcessName.ToLower() == "syndicate" && !p.HasExited && !_ignorePIDs.Contains(p.Id));
            if (game == null)
            {
                return null;
            }

            return game;
        }
    }
}
