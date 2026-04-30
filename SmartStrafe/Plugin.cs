using System;
using System.Reflection;
using System.Runtime.InteropServices;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace SmartStrafe
{
    public class Plugin : IDalamudPlugin
    {
        public static readonly string Version =
            Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.1.0.1";

        public string Name => "SmartStrafe";
        private readonly IPluginLog m_logger;
        private readonly IGameInteropProvider m_gip;
        private readonly IClientState m_clientState;
        private readonly ISigScanner m_scanner;
        private readonly ICondition m_condition;

        public Plugin(
            IPluginLog logger,
            IGameInteropProvider gip,
            ISigScanner scanner,
            IClientState clientState,
            ICondition condition
        )
        {
            m_logger = logger;
            m_gip = gip;
            m_clientState = clientState;
            m_scanner = scanner;
            m_condition = condition;

            m_hook ??= this.Hook(
                m_scanner.ScanText(Signatures.CheckStrafeKeybind),
                new CheckStrafeKeybindDelegate(CheckStrafeKeybind)
            );

            m_hook.Enable();
        }

        private static class Signatures
        {
            internal const string CheckStrafeKeybind = "E8 ?? ?? ?? ?? 84 C0 74 04 41 C6 06 01 BA 44 01 00 00";
        }

        public enum Mode
        {
            Turning,
            Strafing,
            StrafingNoBackpedal,
        }

        private enum Keybind : int
        {
            MoveForward = 321,
            MoveBack = 322,
            TurnLeft = 323,
            TurnRight = 324,
            StrafeLeft = 325,
            StrafeRight = 326,
        }

        private HookWrapper<T> Hook<T>(string signature, T detour, int addressOffset = 0)
            where T : Delegate
        {
            var addr = m_scanner.ScanText(signature);
            var h = m_gip.HookFromAddress(addr + addressOffset, detour);
            return new HookWrapper<T>(h);
        }

        public HookWrapper<T> Hook<T>(nint address, T detour)
            where T : Delegate
        {
            var h = m_gip.HookFromAddress(address, detour);
            var wh = new HookWrapper<T>(h);
            return wh;
        }

        [return: MarshalAs(UnmanagedType.U1)]
        private delegate bool CheckStrafeKeybindDelegate(IntPtr ptr, Keybind keybind);

        private HookWrapper<CheckStrafeKeybindDelegate> m_hook;

        private bool CheckStrafeKeybind(IntPtr ptr, Keybind keybind)
        {
            if (keybind is Keybind.StrafeLeft or Keybind.StrafeRight && !m_clientState.IsGPosing)
            {
                if (
                    (m_hook.Original(ptr, Keybind.TurnLeft) || m_hook.Original(ptr, Keybind.StrafeLeft))
                    && (m_hook.Original(ptr, Keybind.TurnRight) || m_hook.Original(ptr, Keybind.StrafeRight))
                )
                {
                    return true;
                }

                if (m_hook.Original(ptr, Keybind.MoveBack))
                {
                    return false;
                }

                return m_hook.Original(ptr, keybind - 2) || m_hook.Original(ptr, keybind);
            }

            return m_hook.Original(ptr, keybind);
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                m_hook.Disable();
            }
        }
    }
}
