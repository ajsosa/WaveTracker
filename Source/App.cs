﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
//using System.Windows.Forms;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;
using NAudio.CoreAudioApi;
using System.Runtime.InteropServices;
using System.Security.Principal;
using WaveTracker.UI;
using WaveTracker.Rendering;
using WaveTracker.Tracker;
using WaveTracker.Audio;
using System.IO;

namespace WaveTracker {
    public class App : Game {
        static App instance;
        public const string VERSION = "0.2.1";

        private GraphicsDeviceManager graphics;
        private SpriteBatch targetBatch;
        //public static Texture2D channelHeaderSprite;

        public int ScreenWidth = 1920;
        public int ScreenHeight = 1080;
        // public static float ScreenScale { get; } = 2;
        /// <summary>
        /// The height of the app in scaled pixels
        /// </summary>
        public static int WindowHeight { get; private set; }
        /// <summary>
        /// The width of the app in scaled pixels
        /// </summary>
        public static int WindowWidth { get; private set; }
        RenderTarget2D target;


        public static SongSettings SongSettings { get; private set; }
        public static EditSettings EditSettings { get; private set; }
        FramesPanel frameView;
        public Toolbar toolbar;
        int lastPianoKey;
        public static Song newSong;
        public static int pianoInput;
        public static int mouseCursorArrow;
        public static bool VisualizerMode;
        public static Visualization visualization;
        string filename;
        public static PatternEditor PatternEditor { get; private set; }
        public static InstrumentBank InstrumentBank { get; private set; }

        public static InstrumentEditor InstrumentEditor { get; private set; }
        public static WaveBank WaveBank { get; set; }
        public static WaveEditor WaveEditor { get; private set; }


        public static WTModule CurrentModule { get; set; }
        public static int CurrentSongIndex { get; set; }
        public static WTSong CurrentSong { get { return CurrentModule.Songs[CurrentSongIndex]; } }

        public static SettingsProfile Settings { get; private set; }

        public static Dictionary<string, KeyboardShortcut> Shortcuts => Settings.Keyboard.Shortcuts;

        public const int MENUSTRIP_HEIGHT = 10;

        public MenuStrip MenuStrip { get; set; }

        public App(string[] args) {
            instance = this;
            if (args.Length > 0)
                filename = args[0];
            graphics = new GraphicsDeviceManager(this);
            Window.TextInput += Input.ProcessTextInput;
            //graphics.PreferredBackBufferWidth = 1920;  // set this value to the desired width of your window
            //graphics.PreferredBackBufferHeight = 1080 - 72;   // set this value to the desired height of your window
            graphics.ApplyChanges();
            Window.Position = new Point(-8, 0);
            Window.AllowUserResizing = true;
            Window.AllowAltF4 = true;
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
            //Preferences.profile = PreferenceProfile.DefaultProfile;
            //Preferences.ReadFromFile();
            //frameRenderer = new FrameRenderer();
            var form = (System.Windows.Forms.Form)System.Windows.Forms.Control.FromHandle(Window.Handle);
            form.WindowState = System.Windows.Forms.FormWindowState.Maximized;
            form.FormClosing += ClosingForm;
            Settings = new SettingsProfile();
            SettingsProfile.ReadFromDisk(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.Create), "WaveTracker"), "wtpref");
            MidiInput.ReadMidiDevices();
        }

        /// <summary>
        /// Forces a call of update then draw
        /// </summary>
        public static void ForceUpdate() {
            instance.Tick();
        }

        protected override void Initialize() {

            Input.Intialize();

            CurrentModule = new WTModule();
            WaveBank = new WaveBank(510, 18 + MENUSTRIP_HEIGHT);
            ChannelManager.Initialize(WTModule.MAX_CHANNEL_COUNT, WaveBank);
            PatternEditor = new PatternEditor(0, 184 + MENUSTRIP_HEIGHT);
            InstrumentBank = new InstrumentBank(790, 152 + MENUSTRIP_HEIGHT);
            InstrumentBank.Initialize();
            InstrumentEditor = new InstrumentEditor();
            Dialogs.Initialize();
            EditSettings = new EditSettings(312, 18 + MENUSTRIP_HEIGHT);
            toolbar = new Toolbar(2, 0 + MENUSTRIP_HEIGHT);
            WaveEditor = new WaveEditor();
            frameView = new FramesPanel(2, 106 + MENUSTRIP_HEIGHT, 504, 42);
            SongSettings = new SongSettings(2, 18 + MENUSTRIP_HEIGHT);
            AudioEngine.Initialize();

            visualization = new Visualization();
            IsFixedTimeStep = false;

            MenuStrip = new MenuStrip(0, 0, 960, null);
            MenuStrip.AddButton("File", new Menu(new MenuItemBase[] {
                new MenuOption("New", SaveLoad.NewFile),
                new MenuOption("Open...", SaveLoad.OpenFile),
                new MenuOption("Save", SaveLoad.SaveFileVoid),
                new MenuOption("Save As...", SaveLoad.SaveFileAsVoid),
                null,
                new MenuOption("Export as WAV...", Dialogs.exportDialog.Open),
                null,
                new MenuOption("Configuration...", Dialogs.configurationDialog.Open),
                null,
                new MenuOption("Exit", ExitApplication),
            }));
            MenuStrip.AddButton("Edit", new Menu(
                new MenuItemBase[] {
                    new MenuOption("Undo", PatternEditor.Undo, PatternEditor.CanUndo),
                    new MenuOption("Redo", PatternEditor.Redo, PatternEditor.CanRedo),
                    null,
                    new MenuOption("Cut", PatternEditor.Cut, PatternEditor.SelectionIsActive),
                    new MenuOption("Copy", PatternEditor.CopyToClipboard, PatternEditor.SelectionIsActive),
                    new MenuOption("Paste", PatternEditor.PasteFromClipboard, PatternEditor.HasClipboard),
                    new MenuOption("Delete", PatternEditor.Delete, PatternEditor.SelectionIsActive),
                    new MenuOption("Select All", PatternEditor.SelectAll),
                    null,
                    new SubMenu("Pattern", new MenuItemBase[] {
                        new MenuOption("Interpolate", PatternEditor.InterpolateSelection, PatternEditor.SelectionIsActive),
                        new MenuOption("Reverse", PatternEditor.ReverseSelection, PatternEditor.SelectionIsActive),
                        new MenuOption("Replace Instrument", PatternEditor.ReplaceInstrument, PatternEditor.SelectionIsActive),
                        new MenuOption("Humanize Volumes", PatternEditor.Humanize, PatternEditor.SelectionIsActive),
                        null,
						//new MenuOption("Expand", null, PatternEditor.SelectionIsActive),
						//new MenuOption("Shrink", null, PatternEditor.SelectionIsActive),
						//new MenuOption("Stretch...", null, PatternEditor.SelectionIsActive),
						//null,
						new SubMenu("Transpose", new MenuItemBase[] {
                            new MenuOption("Increase note", PatternEditor.IncreaseNote),
                            new MenuOption("Decrease note", PatternEditor.DecreaseNote),
                            new MenuOption("Increase octave", PatternEditor.IncreaseOctave),
                            new MenuOption("Decrease octave", PatternEditor.DecreaseOctave),
                        })
                    }),
                }
            ));
            MenuStrip.AddButton("Song", new Menu(new MenuItemBase[] {
                new MenuOption("Insert frame", PatternEditor.InsertNewFrame),
                new MenuOption("Remove frame", PatternEditor.RemoveFrame),
                new MenuOption("Duplicate frame", PatternEditor.DuplicateFrame),
                null,
                new MenuOption("Move frame left", PatternEditor.MoveFrameLeft),
                new MenuOption("Move frame right", PatternEditor.MoveFrameRight),
            }));
            MenuStrip.AddButton("Module", new Menu(new MenuItemBase[] {
                new MenuOption("Module Settings", Dialogs.moduleSettings.Open),
                null,
                new SubMenu("Cleanup", new MenuItemBase[] {
                        new MenuOption("Remove unused instruments", CurrentModule.RemoveUnusedInstruments),
                        new MenuOption("Remove unused waves", CurrentModule.RemoveUnusedWaves),
                })
            }));
            MenuStrip.AddButton("Instrument", new Menu(new MenuItemBase[] {
                new MenuOption("Add wave instrument", InstrumentBank.AddWave, CurrentModule.Instruments.Count < 100),
                new MenuOption("Add sample instrument", InstrumentBank.AddSample, CurrentModule.Instruments.Count < 100),
                new MenuOption("Duplicate", InstrumentBank.DuplicateInstrument, CurrentModule.Instruments.Count < 100),
                new MenuOption("Remove", InstrumentBank.RemoveInstrument, CurrentModule.Instruments.Count > 1),
                null,
				//new MenuOption("Load from file...", null),
				//new MenuOption("Save to file...", null),
				//null,
				new MenuOption("Rename...", InstrumentBank.Rename),
                new MenuOption("Edit...", InstrumentBank.Edit)
            }));
            MenuStrip.AddButton("Tracker", new Menu(new MenuItemBase[] {
                new MenuOption("Play", Playback.Play),
                new MenuOption("Play from beginning", Playback.PlayFromBeginning),
                new MenuOption("Play from cursor", Playback.PlayFromCursor),
                new MenuOption("Stop", Playback.Stop),
                null,
                new MenuOption("Toggle edit mode", PatternEditor.ToggleEditMode),
                null,
                new MenuOption("Toggle channel", ChannelManager.ToggleCurrentChannel),
                new MenuOption("Solo channel", ChannelManager.SoloCurrentChannel),
                null,
                new MenuOption("Solo channel", ChannelManager.SoloCurrentChannel),

            }));

            base.Initialize();

        }

        protected override void LoadContent() {

            Graphics.font = Content.Load<SpriteFont>("custom_font");
            Graphics.img = Content.Load<Texture2D>("img");

            Graphics.pixel = new Texture2D(GraphicsDevice, 1, 1);
            Graphics.pixel.SetData(new[] { Color.White });
            // TODO: use this.Content to load your game content here
            targetBatch = new SpriteBatch(GraphicsDevice);
            target = new RenderTarget2D(GraphicsDevice, GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width, GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height);
            SaveLoad.NewFile();
            SaveLoad.ReadFrom(filename);
        }

        protected override void Update(GameTime gameTime) {

            Window.Title = SaveLoad.FileNameWithoutExtension + (SaveLoad.IsSaved ? "" : "*") + " [#" + (CurrentSongIndex + 1) + " " + CurrentSong.ToString() + "] - WaveTracker " + VERSION;
            WindowHeight = (Window.ClientBounds.Height / Settings.General.ScreenScale);
            WindowWidth = (Window.ClientBounds.Width / Settings.General.ScreenScale);


            if (IsActive) {
                Input.GetState(gameTime);
                MidiInput.GetInput();
            }
            else {
                Input.windowFocusTimer = 5;
                Input.dialogOpenCooldown = 3;
            }
            //if (Input.GetKeyDown(Keys.OemComma, KeyModifier.CtrlShiftAlt)) {
            //    if (ScreenScale > 1)
            //        Settings.General.ScreenScale -= 0.5f;
            //}
            //if (Input.GetKeyDown(Keys.OemPeriod, KeyModifier.CtrlShiftAlt)) {
            //    ScreenScale += 0.5f;
            //}

            if (Input.dialogOpenCooldown == 0) {
                int mouseX = Mouse.GetState().X;
                int mouseY = Mouse.GetState().Y;
                int width = Window.ClientBounds.Width - 2;
                int height = Window.ClientBounds.Height - 2;
                if (new Rectangle(1, 1, width, height).Contains(mouseX, mouseY))
                    if (mouseCursorArrow == 0) {
                        Mouse.SetCursor(MouseCursor.Arrow);
                    }
                    else {
                        Mouse.SetCursor(MouseCursor.SizeNS);
                        mouseCursorArrow--;
                    }
            }

            Tooltip.Update(gameTime);
            if (Shortcuts["General\\Reset audio"].IsPressedDown) {
                ResetAudio();
            }
            PatternEditor.Update();
            if (!VisualizerMode) {
                WaveBank.Update();
                WaveEditor.Update();
                InstrumentBank.Update();
                InstrumentEditor.Update();
            }
            lastPianoKey = pianoInput;
            pianoInput = -1;
            if (Input.focus == null || WaveEditor.IsOpen || InstrumentEditor.IsOpen) {
                if (MidiInput.GetMidiNote > -1) {
                    pianoInput = MidiInput.GetMidiNote;
                }
                else {

                }
                pianoInput = Helpers.GetPianoInput(PatternEditor.CurrentOctave);
            }
            if (WaveEditor.GetPianoMouseInput() > -1)
                pianoInput = WaveEditor.GetPianoMouseInput();
            if (InstrumentEditor.GetPianoMouseInput() > -1)
                pianoInput = InstrumentEditor.GetPianoMouseInput();

            if (pianoInput >= 0 && lastPianoKey != pianoInput) {
                if (PatternEditor.cursorPosition.Column == CursorColumnType.Note || WaveEditor.IsOpen || InstrumentEditor.IsOpen) {
                    if (!Playback.IsPlaying)
                        AudioEngine.ResetTicks();
                    ChannelManager.previewChannel.SetMacro(InstrumentBank.CurrentInstrumentIndex);
                    ChannelManager.previewChannel.TriggerNote(pianoInput);
                }
            }
            if (pianoInput < 0 && lastPianoKey != pianoInput) {
                if (!Playback.IsPlaying)
                    AudioEngine.ResetTicks();
                ChannelManager.previewChannel.PreviewCut();
            }

            //if (!ChannelManager.previewChannel.waveEnv.toPlay.IsActive)
            //    ChannelManager.previewChannel.SetWave(WaveBank.lastSelectedWave);

            Playback.Update(gameTime);


            if (!VisualizerMode) {
                SongSettings.Update();
                frameView.Update();
                //frameRenderer.Update(gameTime);
                //FrameEditor.Update();
                EditSettings.Update();
            }
            else {
                //sframeRenderer.UpdateChannelHeaders();
            }
            toolbar.Update();
            MenuStrip.Update();
            Dialogs.Update();

            ContextMenu.Update();
            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime) {
            graphics.PreferredBackBufferWidth = Window.ClientBounds.Width;
            graphics.PreferredBackBufferHeight = Window.ClientBounds.Height;
            graphics.ApplyChanges();

            GraphicsDevice.SetRenderTarget(target);
            GraphicsDevice.Clear(UIColors.black);


            // TODO: Add your drawing code here
            targetBatch.Begin(SpriteSortMode.Deferred, new BlendState {
                ColorSourceBlend = Blend.SourceAlpha,
                ColorDestinationBlend = Blend.InverseSourceAlpha,
                AlphaSourceBlend = Blend.One,
                AlphaDestinationBlend = Blend.InverseSourceAlpha,
            }, SamplerState.PointClamp, DepthStencilState.Default, RasterizerState.CullNone);

            Rendering.Graphics.batch = targetBatch;

            if (!VisualizerMode) {
                // draw frame editor
                //frameRenderer.DrawFrame(Song.currentSong.frames[FrameEditor.currentFrame], FrameEditor.cursorRow, FrameEditor.cursorColumn);

                PatternEditor.Draw();

                // draw instrument bank
                InstrumentBank.Draw();

                // draw wave bank
                WaveBank.Draw();

                // draw edit settings
                EditSettings.Draw();

                // draw frame view
                frameView.Draw();

                // draw song settings
                SongSettings.Draw();

                // draw click position
                //Rendering.Graphics.DrawRect(Input.lastClickLocation.X, Input.lastClickLocation.Y, 1, 1, Color.Red);
                //Rendering.Graphics.DrawRect(Input.lastClickReleaseLocation.X, Input.lastClickReleaseLocation.Y, 1, 1, Color.DarkRed);
            }
            else {
                visualization.Draw();
            }
            toolbar.Draw();
            MenuStrip.Draw();

            if (!VisualizerMode) {
                WaveEditor.Draw();
                InstrumentEditor.Draw();

            }

            Dialogs.Draw();
            Dropdown.DrawCurrentMenu();
            DropdownButton.DrawCurrentMenu();
            ContextMenu.Draw();
            Tooltip.Draw();
            //if (MidiInput.currentlyHeldDownNotes != null) {
            //    for (int i = 0; i < MidiInput.currentlyHeldDownNotes.Count; ++i) {
            //        Graphics.Write("note: " + Helpers.MIDINoteToText(MidiInput.currentlyHeldDownNotes[i]) + " " + (int)(99 * (MidiInput.GetVelocity / 127f)), 20, 20 + i * 10, Color.Red);
            //    }
            //}
            //int y = 10;
            //foreach (MMDevice k in audioEngine.devices)
            //{
            //    Rendering.Graphics.Write(k.DeviceFriendlyName, 2, y, Color.Red);
            //    y += 10;

            //}
            //Rendering.Graphics.Write("AudioStatus: " + audioEngine.wasapiOut.PlaybackState.ToString(), 2, 2, Color.Red);
            //Rendering.Graphics.Write("filename: " + filename, 2, 12, Color.Red);
            //Rendering.Graphics.Write("FPS: " + 1 / gameTime.ElapsedGameTime.TotalSeconds, 2, 2, Color.Red);

            Graphics.Write(MidiInput.GetMidiNote + ", " + pianoInput, 2, 250, Color.Red);
            Graphics.Write("@" + Input.focus + "", 2, 260, Color.Red);

            targetBatch.End();



            //set rendering back to the back buffer
            GraphicsDevice.SetRenderTarget(null);
            //render target to back buffer
            targetBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, (Settings.General.ScreenScale % 1) == 0 ? SamplerState.PointClamp : SamplerState.LinearClamp, DepthStencilState.Default, RasterizerState.CullNone);
            targetBatch.Draw(target, new Rectangle(0, 0, GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width * Settings.General.ScreenScale, GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height * Settings.General.ScreenScale), Color.White);
            if (VisualizerMode && Input.focus == null) {
                try {
                    visualization.DrawPiano(visualization.states);
                } catch {
                    //visualization.DrawPiano(visualization.statesPrev);
                }
                visualization.DrawOscilloscopes();
            }
            targetBatch.End();

            base.Draw(gameTime);
        }

        /// <summary>
        /// Resets all channels and audio
        /// </summary>
        public static void ResetAudio() {
            ChannelManager.Reset();
            MidiInput.ReadMidiDevices();
            AudioEngine.Reset();
        }

        /// <summary>
        /// Closes WaveTracker
        /// </summary>
        public static void ExitApplication() {
            System.Windows.Forms.Form form = (System.Windows.Forms.Form)System.Windows.Forms.Control.FromHandle(instance.Window.Handle);
            form.Close();
        }

        /// <summary>
        /// Called before the app closes
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void ClosingForm(object sender, System.ComponentModel.CancelEventArgs e) {
            if (!SaveLoad.IsSaved) {
                e.Cancel = true;
                SaveLoad.DoSaveChangesDialog(UnsavedChangesCallback);
            }

        }

        /// <summary>
        /// Called
        /// </summary>
        /// <param name="result"></param>
        void UnsavedChangesCallback(string result) {
            if (result == "Yes") {
                SaveLoad.SaveFile();
            }
            else if (result == "Cancel") {
                return;
            }
            Exit();
        }

        protected override void OnExiting(object sender, EventArgs args) {
            Debug.WriteLine("Closing WaveTracker...");
            AudioEngine.Stop();
            MidiInput.Stop();
            base.OnExiting(sender, args);
        }
    }
}