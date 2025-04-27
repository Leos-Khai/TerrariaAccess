using Terraria;
using Terraria.ModLoader;
using Terraria.UI;
using Terraria.GameInput; // Needed for ProcessTriggers in AccessibilityPlayer
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria.Localization;
using CrossSpeak; // Assuming CrossSpeak namespace
using System.Diagnostics; // For Stopwatch

namespace TerrariaAccess
{
  public class TerrariaAccess : Mod
  {
    public static ModKeybind TestKeybind { get; private set; }

    public override void Load()
    {
      // Initialize CrossSpeak when the mod loads
      // Log any errors during initialization
      TestKeybind = KeybindLoader.RegisterKeybind(this, "TestCrossSpeak", "T"); // Register 'T' key
      try
      {
        CrossSpeakManager.Instance.Initialize();
        CrossSpeakManager.Instance.Speak("This is some word that will be speaking for a long time", true);

        Logger.Info("CrossSpeak initialized successfully.");
      }
      catch (System.Exception e)
      {
        Logger.Error("Failed to initialize CrossSpeak: " + e.Message);
      }
    }

    public override void Unload()
    {
      // Clean up CrossSpeak resources
      TestKeybind = null; // Unload the keybind
      try
      {
        CrossSpeakManager.Instance.Close();
        Logger.Info("CrossSpeak closed successfully.");
      }
      catch (System.Exception e)
      {
        Logger.Error("Failed to close CrossSpeak: " + e.Message);
      }
    }
  }

  public class MainMenuAccessibilitySystem : ModSystem
  {
    private string lastSpokenText = null;
    // private int lastHoveredButtonIndex = -1; // Removed, wasn't used

    // Debounce timer variables
    private Stopwatch hoverTimer = new Stopwatch();
    private string pendingSpeakText = null;
    private const long DebounceMilliseconds = 300; // Adjust as needed (e.g., 300ms)

    // Helper function for speaking with error handling
    private void SpeakText(string text, bool interrupt = false)
    {
      if (string.IsNullOrEmpty(text)) return;

      try
      {
        CrossSpeakManager.Instance.Speak(text, interrupt);
        // Optional: Log success if needed for debugging
        // ModContent.GetInstance<TerrariaAccess>().Logger.Debug($"Successfully spoke: {text}");
      }
      catch (System.Exception e)
      {
        ModContent.GetInstance<TerrariaAccess>().Logger.Error($"CrossSpeak failed: {e.Message}");
        // Consider adding logic to disable speech temporarily if errors persist
      }
    }

    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
    {
      if (!Main.gameMenu)
      {
        // Reset state and stop any pending speech when leaving menu
        if (lastSpokenText != null || pendingSpeakText != null)
        {
          lastSpokenText = null;
          pendingSpeakText = null;
          hoverTimer.Reset();
          // Optional: Stop current speech if desired
          // try { CrossSpeakManager.Instance.Stop(); } catch { /* Ignore error */ }
        }
        return;
      }

      int mouseTextIndex = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Mouse Text"));
      if (mouseTextIndex != -1)
      {
        layers.Insert(mouseTextIndex, new LegacyGameInterfaceLayer(
            "TerrariaAccess: Main Menu Announcer",
            delegate
            {
              ProcessMainMenuHovers();
              // Process the debounce timer outside the main hover logic
              ProcessDebounce();
              return true;
            },
            InterfaceScaleType.UI)
        );
      }
    }

    private void ProcessMainMenuHovers()
    {
      Vector2 mousePosition = Main.MouseScreen;
      string currentlyHoveredText = null;
      // int currentHoveredButtonIndex = -1; // Removed

      // TODO: This list is still hardcoded. A more robust solution would involve
      // inspecting Main.menuItem array or using reflection, but that's more complex.
      // For now, we add common mod buttons with approximate positions.
      var menuButtons = new List<(Rectangle area, int langIndex, string fallbackText)>
        {
            // Vanilla Buttons (adjust Y offsets based on actual layout)
            (new Rectangle(Main.screenWidth / 2 - 100, 200 + 0 * 45, 200, 40), 1, "Single Player"),
            (new Rectangle(Main.screenWidth / 2 - 100, 200 + 1 * 45, 200, 40), 2, "Multiplayer"),
            // (new Rectangle(Main.screenWidth / 2 - 100, 200 + 2 * 45, 200, 40), ?, "Achievements"), // Index varies
            (new Rectangle(Main.screenWidth / 2 - 100, 200 + 2 * 45, 200, 40), 131, "Workshop"), // Index might vary
            (new Rectangle(Main.screenWidth / 2 - 100, 200 + 3 * 45, 200, 40), 127, "Mods"), // Common mod button
            (new Rectangle(Main.screenWidth / 2 - 100, 200 + 4 * 45, 200, 40), 128, "Mod Browser"), // Common mod button
            (new Rectangle(Main.screenWidth / 2 - 100, 200 + 5 * 45, 200, 40), 129, "Mod Sources"), // Common mod button
            (new Rectangle(Main.screenWidth / 2 - 100, 200 + 6 * 45, 200, 40), 6, "Settings"),
            (new Rectangle(Main.screenWidth / 2 - 100, 200 + 7 * 45, 200, 40), 7, "Exit")
            // Note: Y positions (e.g., 200 + N * 45) are highly approximate and depend
            // on screen resolution, UI scale, and active mods. A dynamic approach is better.
        };

      for (int i = 0; i < menuButtons.Count; i++)
      {
        var (area, langIndex, fallback) = menuButtons[i];

        // --- Corrected Scaling ---
        // Calculate the scaled position and size based on the top-left corner and dimensions
        // Note: This assumes buttons are positioned relative to screen center/top-left.
        // Centered elements might need slightly different scaling logic for X/Y.
        // We also need to account for the vertical offset based on Main.menuMode (though it's often 0 in main menu)
        float scale = Main.UIScale;
        int yOffset = (int)(Main.menuMode * 0); // Placeholder adjustment if needed

        Rectangle scaledArea = new Rectangle(
            (int)(area.X * scale), // Scale X
            (int)((area.Y + yOffset) * scale), // Scale Y (with potential offset)
            (int)(area.Width * scale), // Scale Width
            (int)(area.Height * scale) // Scale Height
        );
        // --- End Corrected Scaling ---


        if (scaledArea.Contains(mousePosition.ToPoint()))
        {
          // --- Safer Language Handling ---
          string langKey = $"LegacyMenu.{langIndex}";
          string altLangKey = $"UI.Button{langIndex}"; // Another common pattern
          string menuLangKey = $"Menu.{langIndex}"; // Yet another pattern

          if (Language.Exists(langKey))
            currentlyHoveredText = Language.GetTextValue(langKey);
          else if (Language.Exists(altLangKey))
            currentlyHoveredText = Language.GetTextValue(altLangKey);
          else if (Language.Exists(menuLangKey))
            currentlyHoveredText = Language.GetTextValue(menuLangKey);
          else
          {
            currentlyHoveredText = fallback; // Use the provided fallback text
            ModContent.GetInstance<TerrariaAccess>().Logger.Warn($"Lang key not found for index {langIndex} (tried LegacyMenu, UI.Button, Menu). Using fallback: '{fallback}'");
          }
          // --- End Safer Language Handling ---

          // currentHoveredButtonIndex = i; // Removed
          break;
        }
      }

      // --- Debounce Logic ---
      if (currentlyHoveredText != lastSpokenText) // If hover target changed
      {
        if (currentlyHoveredText != null)
        {
          // Set text to be spoken after delay and start/restart timer
          pendingSpeakText = currentlyHoveredText;
          hoverTimer.Restart();
        }
        else
        {
          // Moved off a button, clear pending text and stop timer
          pendingSpeakText = null;
          hoverTimer.Reset();
          // Optional: Stop current speech immediately if desired
          // SpeakText(null, true); // Or CrossSpeakManager.Instance.Stop();
        }
        lastSpokenText = currentlyHoveredText; // Update last *intended* text immediately
      }
      // --- End Debounce Logic ---
    }

    private void ProcessDebounce()
    {
      // If timer is running and exceeds delay, speak the pending text
      if (hoverTimer.IsRunning && hoverTimer.ElapsedMilliseconds >= DebounceMilliseconds)
      {
        SpeakText(pendingSpeakText, true); // Speak the text that was pending
        pendingSpeakText = null; // Clear pending text
        hoverTimer.Reset(); // Stop the timer
      }
    }
  }

  public class AccessibilityPlayer : ModPlayer
  {
    // Add a counter for logging frequency control if needed
    // private int triggerLogCounter = 0;

    public override void ProcessTriggers(Terraria.GameInput.TriggersSet triggersSet) // Added namespace for clarity
    {
      // Optional: Log periodically to confirm ProcessTriggers is running without spamming
      /*
      triggerLogCounter++;
      if (triggerLogCounter % 300 == 0) { // Log every 5 seconds (approx 60 updates/sec)
          Mod.Logger.Debug("AccessibilityPlayer.ProcessTriggers is running.");
      }
      */

      // Check if our custom keybind was pressed (with null check)
      if (TerrariaAccess.TestKeybind != null && TerrariaAccess.TestKeybind.JustPressed)
      {
        // Use ModContent logger instance for safety
        ModContent.GetInstance<TerrariaAccess>().Logger.Info("TestKeybind detected as JustPressed."); // Log detection
        try
        {
          // Speak the test message, interrupting any current speech
          CrossSpeakManager.Instance.Speak("Testing from CrossSpeak", true);
          // Log success
          ModContent.GetInstance<TerrariaAccess>().Logger.Info("CrossSpeakManager.Speak called successfully.");
        }
        catch (System.Exception e)
        {
          // Log any errors if speaking fails
          ModContent.GetInstance<TerrariaAccess>().Logger.Error($"CrossSpeak failed on keybind: {e.Message}");
        }
      }
    }
  }
}