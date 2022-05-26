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
using System.Threading.Tasks;

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

        [DllImport("user32")]
        public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
        public const uint WM_COMMAND = 0x0111;
        public const uint WM_PASTE = 0x0302;
        [DllImport("user32")]
        public static extern bool PostMessage(IntPtr hWnd, uint Msg, int wParam, int lParam);

        public SettingEntry<Blish_HUD.Input.KeyBinding> ToggleModule_Key;
        public SettingEntry<bool> ShowCornerIcon;

        public string CultureString;
        public TextureManager TextureManager;
        public Ticks Ticks = new Ticks();

        private CornerIcon cornerIcon;

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

            DataLoaded = false;
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
            Ticks.global += gameTime.ElapsedGameTime.TotalMilliseconds;

            if (Ticks.global > 25 && ModuleActive)
            {
                Ticks.global = 0;

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

                Zoom = Mumble.PlayerCamera.FieldOfView;
            }
        }

        protected override void Unload()
        {

            TextureManager?.Dispose();
            TextureManager = null;

            ToggleModule_Key.Value.Activated -= ToggleModule;
            ShowCornerIcon.SettingChanged -= ShowCornerIcon_SettingChanged;

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