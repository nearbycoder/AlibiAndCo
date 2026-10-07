using AlibiCo.Logic;
using NUnit.Framework;

namespace AlibiCo.Tests
{
    /// <summary>
    /// The pad prompts name the buttons of the pad in use (Logic/PadLabels): which family a device
    /// belongs to, from the names the Input System and the browsers give it, and the renaming itself.
    /// </summary>
    public class PadLabelsTests
    {
        [TestCase("DualSenseGamepadHID")]
        [TestCase("DualShock4GamepadHID")]
        [TestCase("DualShockGamepad")]
        [TestCase(null, "Sony Interactive Entertainment", "DualSense Wireless Controller")]
        [TestCase(null, "Sony Interactive Entertainment", "Wireless Controller")]   // a DualShock 4 on Linux
        [TestCase("WebGLGamepad", null, "DualSense Wireless Controller (STANDARD GAMEPAD Vendor: 054c Product: 0ce6)")]   // Chromium
        [TestCase("WebGLGamepad", null, "054c-09cc-Wireless Controller")]   // Firefox, a DualShock 4
        public void PlayStation(params string[] names) => Assert.AreEqual(PadFamily.PlayStation, PadLabels.Detect(names));

        [TestCase("SwitchProControllerHID")]
        [TestCase(null, "Nintendo Co., Ltd.", "Pro Controller")]
        [TestCase("WebGLGamepad", null, "Pro Controller (STANDARD GAMEPAD Vendor: 057e Product: 2009)")]
        [TestCase("WebGLGamepad", null, "057e-2009-Pro Controller")]
        public void Nintendo(params string[] names) => Assert.AreEqual(PadFamily.Nintendo, PadLabels.Detect(names));

        [TestCase("XInputControllerLinux")]
        [TestCase("Gamepad", null, null)]
        [TestCase("WebGLGamepad", null, "Xbox 360 Controller (XInput STANDARD GAMEPAD)")]
        [TestCase("WebGLGamepad", null, "045e-02ea-Microsoft X-Box One S pad")]
        [TestCase(null, "8BitDo", "8BitDo Pro 2")]
        [TestCase(null, null, "Wireless Controller")]   // no vendor: the prompts' own (Xbox) names
        public void Xbox(params string[] names) => Assert.AreEqual(PadFamily.Xbox, PadLabels.Detect(names));

        [Test]
        public void NoNames() => Assert.AreEqual(PadFamily.Xbox, PadLabels.Detect(null));

        const string Prompt = "<b>[A]</b> pins   ·   <b>[B]</b> back   ·   <b>[X]</b> hint   ·   <b>[Y]</b> notebook   ·   <b>[LB] [RB]</b> jump   ·   <b>[Start]</b> menu";

        [Test]
        public void XboxPromptsAreUnchanged() => Assert.AreEqual(Prompt, PadLabels.Localize(Prompt, PadFamily.Xbox));

        [Test]
        public void PlayStationShapes()
        {
            var s = PadLabels.Localize(Prompt, PadFamily.PlayStation);
            Assert.AreEqual("<b>[✕]</b> pins   ·   <b>[○]</b> back   ·   <b>[□]</b> hint   ·   <b>[△]</b> notebook   ·   <b>[L1] [R1]</b> jump   ·   <b>[Options]</b> menu", s);
            Assert.IsFalse(PadLabels.HasXboxTokens(s));
        }

        [Test]
        public void NintendoNamesTheButtonInThatPlace()
        {
            // The bottom button is labelled B on a Switch pad, the right one A: they swap at once, not one after the other.
            Assert.AreEqual("<b>[B]</b> pins   ·   <b>[A]</b> back   ·   <b>[Y]</b> hint   ·   <b>[X]</b> notebook   ·   <b>[L] [R]</b> jump   ·   <b>[+]</b> menu",
                PadLabels.Localize(Prompt, PadFamily.Nintendo));
        }

        [Test]
        public void OnlyBracketedNamesChange()
        {
            Assert.AreEqual("A Start, B, LB and [✕]", PadLabels.Localize("A Start, B, LB and [A]", PadFamily.PlayStation));
            Assert.AreEqual("", PadLabels.Localize("", PadFamily.PlayStation));
            Assert.IsNull(PadLabels.Localize(null, PadFamily.Nintendo));
        }
    }
}
