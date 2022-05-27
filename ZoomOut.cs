using Blish_HUD;
using Blish_HUD.Controls;
using Blish_HUD.Modules;
using Blish_HUD.Modules.Managers;
using Blish_HUD.Settings;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using System.ComponentModel.Composition;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using UiSize = Gw2Sharp.Mumble.Models.UiSize;

namespace Kenedia.Modules.ZoomOut
{
    [Export(typeof(Module))]
    public class ZoomOut : Module
    {
        internal static ZoomOut ModuleInstance;
        public static readonly Logger Logger = Logger.GetLogger<ZoomOut>();

        #region Service Managers

        internal SettingsManager SettingsManager => this.ModuleParameters.SettingsManager;
        internal ContentsManager ContentsManager => this.ModuleParameters.ContentsManager;
        internal DirectoriesManager DirectoriesManager => this.ModuleParameters.DirectoriesManager;
        internal Gw2ApiManager Gw2ApiManager => this.ModuleParameters.Gw2ApiManager;

        #endregion

        [ImportingConstructor]
        public ZoomOut([Import("ModuleParameters")] ModuleParameters moduleParameters) : base(moduleParameters)
        {
            ModuleInstance = this;
        }

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetWindowRect(IntPtr hWnd, ref RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        public SettingEntry<Blish_HUD.Input.KeyBinding> ToggleModule_Key;
        public SettingEntry<Blish_HUD.Input.KeyBinding> ManualMaxZoomOut;
        public SettingEntry<bool> ShowCornerIcon;

        public string CultureString;
        public TextureManager TextureManager;
        public Ticks Ticks = new Ticks();

        private CornerIcon cornerIcon;

        private int MumbleTick;
        private Point Resolution;
        private bool InGame;
        private float Zoom;
        private int ZoomTicks = 0;

        private bool _DataLoaded;
        public bool ModuleActive;
        public bool DataLoaded
        {
            get => _DataLoaded;
            set
            {
                _DataLoaded = value;
                if (value) ModuleInstance.OnDataLoaded();
            }
        }

        public event EventHandler DataLoaded_Event;
        void OnDataLoaded()
        {
            this.DataLoaded_Event?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler LanguageChanged;
        public void OnLanguageChanged(object sender, EventArgs e)
        {
            this.LanguageChanged?.Invoke(this, EventArgs.Empty);
        }

        protected override void DefineSettings(SettingCollection settings)
        {
            ToggleModule_Key = settings.DefineSetting(nameof(ToggleModule_Key),
                                                      new Blish_HUD.Input.KeyBinding(ModifierKeys.Ctrl, Keys.NumPad0),
                                                      () => string.Format(Strings.common.Toggle, Name));

            ManualMaxZoomOut = settings.DefineSetting(nameof(ManualMaxZoomOut),
                                                      new Blish_HUD.Input.KeyBinding(Keys.None),
                                                      () => Strings.common.ManualMaxZoomOut_Name,
                                                      () => Strings.common.ManualMaxZoomOut_Tooltip);

            ShowCornerIcon = settings.DefineSetting(nameof(ShowCornerIcon),
                                                      true,
                                                      () => Strings.common.ShowCorner_Name,
                                                      () => Strings.common.ShowCorner_Tooltip);
        }

        protected override void Initialize()
        {
            Logger.Info($"Starting {Name} v." + Version.BaseVersion());

            ToggleModule_Key.Value.Enabled = true;
            ToggleModule_Key.Value.Activated += ToggleModule;
            ShowCornerIcon.SettingChanged += ShowCornerIcon_SettingChanged;

            ManualMaxZoomOut.Value.Enabled = true;
            ManualMaxZoomOut.Value.Activated += ManualMaxZoomOut_Triggered;

            DataLoaded = false;
        }

        private void ManualMaxZoomOut_Triggered(object sender, EventArgs e)
        {
            ZoomTicks = 40;
        }

        private void ToggleModule(object sender, EventArgs e)
        {
            ModuleActive = !ModuleActive;
            if (cornerIcon != null)
            {
                cornerIcon.Icon = ModuleActive ? TextureManager.getIcon(_Icons.ModuleIcon_Active) : TextureManager.getIcon(_Icons.ModuleIcon);
                cornerIcon.HoverIcon = ModuleActive ? TextureManager.getIcon(_Icons.ModuleIcon_ActiveHovered) : TextureManager.getIcon(_Icons.ModuleIcon_HoveredWhite);
            }
        }

        protected override async Task LoadAsync()
        {
        }

        protected override void OnModuleLoaded(EventArgs e)
        {
            TextureManager = new TextureManager();

            cornerIcon = new CornerIcon()
            {
                Icon = TextureManager.getIcon(_Icons.ModuleIcon),
                HoverIcon = TextureManager.getIcon(_Icons.ModuleIcon_HoveredWhite),
                BasicTooltipText = string.Format(Strings.common.Toggle, $"{Name}"),
                Parent = GameService.Graphics.SpriteScreen,
                Visible =  ShowCornerIcon.Value,
            };

            cornerIcon.Click += CornerIcon_Click;

            // Base handler must be called
            base.OnModuleLoaded(e);
        }


        private void CornerIcon_Click(object sender, Blish_HUD.Input.MouseEventArgs e)
        {
            ToggleModule(null, null);
        }


        private void ShowCornerIcon_SettingChanged(object sender, ValueChangedEventArgs<bool> e)
        {
            if (cornerIcon != null) cornerIcon.Visible = e.NewValue;
        }

        protected override void Update(GameTime gameTime)
        {
            if (ModuleActive)
            {
                Ticks.global = gameTime.TotalGameTime.TotalMilliseconds;

                var Mumble = GameService.Gw2Mumble;

                if(Zoom < Mumble.PlayerCamera.FieldOfView)
                {
                    ZoomTicks += 2;
                }
                else if (ZoomTicks > 0)
                {
                    Blish_HUD.Controls.Intern.Mouse.RotateWheel(-25);
                    ZoomTicks -= 1;
                }
                var mouse = Mouse.GetState();
                var mouseState =  (mouse.LeftButton == ButtonState.Released) ? ButtonState.Released : ButtonState.Pressed;

                if(mouseState == ButtonState.Pressed || GameService.Graphics.Resolution != Resolution)
                {
                    Resolution = GameService.Graphics.Resolution;
                    MumbleTick = Mumble.Tick + 5;
                    return;
                }                

                if (!GameService.GameIntegration.Gw2Instance.IsInGame && InGame && Mumble.Tick > MumbleTick)
                {
                    Blish_HUD.Controls.Intern.Keyboard.Stroke(Blish_HUD.Controls.Extern.VirtualKeyShort.ESCAPE, false);
                    Blish_HUD.Controls.Intern.Mouse.Click(Blish_HUD.Controls.Intern.MouseButton.LEFT, 5, 5);

                    MumbleTick = Mumble.Tick + 1;
                }
                InGame = GameService.GameIntegration.Gw2Instance.IsInGame;

                Zoom = Mumble.PlayerCamera.FieldOfView;
            }
        }

        protected override void Unload()
        {

            TextureManager?.Dispose();
            TextureManager = null;

            ToggleModule_Key.Value.Activated -= ToggleModule;
            ShowCornerIcon.SettingChanged -= ShowCornerIcon_SettingChanged;

            ManualMaxZoomOut.Value.Enabled = false;
            ManualMaxZoomOut.Value.Activated -= ManualMaxZoomOut_Triggered;

            cornerIcon?.Dispose();
            if(cornerIcon != null) cornerIcon.Click -= CornerIcon_Click;
            OverlayService.Overlay.UserLocale.SettingChanged -= UserLocale_SettingChanged;
            
            DataLoaded = false;
            ModuleInstance = null;
        }

        public async Task Fetch_APIData(bool force = false)
        {
        }

        private async void UserLocale_SettingChanged(object sender, ValueChangedEventArgs<Gw2Sharp.WebApi.Locale> e)
        {
            cornerIcon.BasicTooltipText = string.Format(Strings.common.Toggle, $"{Name}");

            OnLanguageChanged(null, null);
        }
    }
}