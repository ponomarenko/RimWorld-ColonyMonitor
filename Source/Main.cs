using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Verse.Sound;
using Verse.Noise;
using Verse.Grammar;
using RimWorld;
using RimWorld.Planet;

// *Uncomment for Harmony*
// using System.Reflection;
// using HarmonyLib;

namespace ColonyMonitor
{
    [DefOf]
    public class ColonyMonitorDefOf
    {
        public static LetterDef success_letter;
    }

    public class MyMapComponent : MapComponent
    {
        public MyMapComponent(Map map) : base(map) { }
        public override void FinalizeInit()
        {
            Messages.Message("Success", null, MessageTypeDefOf.PositiveEvent);
            Find.LetterStack.ReceiveLetter(new TaggedString("Success"), new TaggedString("Success message"), ColonyMonitorDefOf.success_letter, "", 0);
        }
    }

    [StaticConstructorOnStartup]
    public static class Start
    {
        static Start()
        {
            Log.Message("Mod template loaded successfully!");

            // *Uncomment for Harmony*
            // Harmony harmony = new Harmony("Template");
            // harmony.PatchAll( Assembly.GetExecutingAssembly() );
        }
    }

    // *Uncomment for Harmony*
    // [HarmonyPatch(typeof(LetterStack), "ReceiveLetter")]
    // [HarmonyPatch(new Type[] {typeof(TaggedString), typeof(TaggedString), typeof(LetterDef), typeof(string), typeof(int), typeof(bool)})]
    public static class LetterTextChange
    {
        public static bool Prefix(ref TaggedString text)
        {
            text += new TaggedString(" with harmony");
            return true;
        }
    }

    public class PawnSexLabelMapComponent : MapComponent
    {
        public PawnSexLabelMapComponent(Map map) : base(map) { }

        public override void MapComponentOnGUI()
        {
            foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
            {
                if (!pawn.Spawned || pawn.Position == IntVec3.Invalid) continue;

                Vector3 drawPos = GenMapUI.LabelDrawPosFor(pawn, -1.2f);
                string sexLabel = pawn.gender == Gender.Male ? "♂" : pawn.gender == Gender.Female ? "♀" : "?";
                GenMapUI.DrawThingLabel(drawPos, sexLabel, Color.white);
            }
        }
    }

    public static class UIConstants
    {
        public static float ButtonWidth = 220f;
        public static float ButtonHeight = 32f;
        public static float WindowWidth = 420f;
        public static float WindowHeight = 400f;
        public static float Margin = 10f;
        public static float TopOffset = 200f;
    }

    public class ClearOldLettersMapComponent : MapComponent
    {
        public ClearOldLettersMapComponent(Map map) : base(map) { }

        public override void MapComponentOnGUI()
        {
            Rect buttonRect = new Rect(
                UI.screenWidth - UIConstants.ButtonWidth - UIConstants.Margin, 
                UIConstants.TopOffset,
                UIConstants.ButtonWidth,
                UIConstants.ButtonHeight
            );

            if (Widgets.ButtonText(buttonRect, "Clear notifications"))
            {
                var toRemove = new List<Letter>();
                foreach (var letter in Find.LetterStack.LettersListForReading)
                {
                    if (letter.def == ColonyMonitorDefOf.success_letter || letter.def == LetterDefOf.NegativeEvent)
                        toRemove.Add(letter);
                }
                foreach (var letter in toRemove)
                {
                    Find.LetterStack.RemoveLetter(letter);
                }
                Messages.Message("Old notifications cleared.", MessageTypeDefOf.PositiveEvent);
            }
        }
    }

    public class CriticalNeedsWindowMapComponent : MapComponent
    {
        private bool showWindow = false;
        private Vector2 scrollPos = Vector2.zero;

        public CriticalNeedsWindowMapComponent(Map map) : base(map) { }

        public override void MapComponentOnGUI()
        {
            float buttonY = UIConstants.TopOffset + UIConstants.ButtonHeight + UIConstants.Margin;
            Rect buttonRect = new Rect(
                UI.screenWidth - UIConstants.ButtonWidth - UIConstants.Margin,
                buttonY,
                UIConstants.ButtonWidth,
                UIConstants.ButtonHeight
            );

            if (Widgets.ButtonText(buttonRect, showWindow ? "Hide critical needs" : "Show critical needs"))
            {
                showWindow = !showWindow;
            }

            if (showWindow)
            {
                if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
                {
                    showWindow = false;
                    Event.current.Use();
                }
                DrawCriticalNeedsWindow();
            }
        }

        private void DrawCriticalNeedsWindow()
        {
            float windowY = UIConstants.TopOffset + (UIConstants.ButtonHeight + UIConstants.Margin) * 2;
            Rect windowRect = new Rect(
                UI.screenWidth - UIConstants.WindowWidth - UIConstants.Margin,
                windowY,
                UIConstants.WindowWidth,
                UIConstants.WindowHeight
            );

            // Додаємо кнопку закриття
            Rect closeRect = new Rect(
                windowRect.x + windowRect.width - 24f,
                windowRect.y + 4f,
                20f,
                20f
            );

            Widgets.DrawWindowBackground(windowRect);
            if (Widgets.ButtonText(closeRect, "×"))
            {
                showWindow = false;
                return;
            }

            Rect innerRect = new Rect(windowRect.x + 10f, windowRect.y + 40f, UIConstants.WindowWidth - 20f, UIConstants.WindowHeight - 50f);

            var pawns = map.mapPawns.FreeColonistsSpawned;
            var pawnNeeds = new List<(Pawn pawn, Need need, float pct)>();

            foreach (var pawn in pawns)
            {
                if (pawn.needs == null) continue;
                var criticalNeed = pawn.needs.AllNeeds
                    .Where(n => n.CurLevelPercentage < 0.4f && n.CurLevelPercentage > 0f)
                    .OrderBy(n => n.CurLevelPercentage)
                    .FirstOrDefault();

                if (criticalNeed != null && criticalNeed.CurLevelPercentage < 0.4f)
                {
                    pawnNeeds.Add((pawn, criticalNeed, criticalNeed.CurLevelPercentage));
                }
            }

            // Сортуємо: найнижчий рівень потреби — найвищий пріоритет
            pawnNeeds = pawnNeeds.OrderBy(t => t.pct).ToList();

            float lineHeight = 32f;
            float y = 0f;
            Widgets.BeginScrollView(innerRect, ref scrollPos, new Rect(0f, 0f, innerRect.width - 16f, pawnNeeds.Count * lineHeight));
            foreach (var (pawn, need, pct) in pawnNeeds)
            {
                string label = $"{pawn.LabelShortCap}: {need.LabelCap} ({(int)(pct * 100)}%)";
                Color color = pct < 0.2f ? Color.red : (pct < 0.4f ? Color.yellow : Color.white);
                Rect lineRect = new Rect(0f, y, innerRect.width - 16f, lineHeight);
                GUI.color = color;
                Widgets.Label(lineRect, label);
                GUI.color = Color.white;
                y += lineHeight;
            }
            Widgets.EndScrollView();
        }

        public class PawnActivityWindowMapComponent : MapComponent
        {
            private bool showWindow = false;
            private Vector2 scrollPos = Vector2.zero;

            public PawnActivityWindowMapComponent(Map map) : base(map) { }

            public override void MapComponentOnGUI()
            {
                float buttonY = UIConstants.TopOffset + (UIConstants.ButtonHeight + UIConstants.Margin) * 2;
                Rect buttonRect = new Rect(
                    UI.screenWidth - UIConstants.ButtonWidth - UIConstants.Margin,
                    buttonY,
                    UIConstants.ButtonWidth,
                    UIConstants.ButtonHeight
                );

                if (Widgets.ButtonText(buttonRect, showWindow ? "Hide pawn activities" : "Show pawn activities"))
                {
                    showWindow = !showWindow;
                }

                if (showWindow)
                {
                    if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
                    {
                        showWindow = false;
                        Event.current.Use();
                    }
                    DrawPawnActivityWindow();
                }
            }

            private void DrawPawnActivityWindow()
            {
                float windowY = UIConstants.TopOffset + (UIConstants.ButtonHeight + UIConstants.Margin) * 3;
                Rect windowRect = new Rect(
                    UI.screenWidth - UIConstants.WindowWidth - UIConstants.Margin,
                    windowY,
                    UIConstants.WindowWidth,
                    UIConstants.WindowHeight
                );

                // Додаємо кнопку закриття
                Rect closeRect = new Rect(
                    windowRect.x + windowRect.width - 24f,
                    windowRect.y + 4f,
                    20f,
                    20f
                );

                Widgets.DrawWindowBackground(windowRect);
                if (Widgets.ButtonText(closeRect, "×"))
                {
                    showWindow = false;
                    return;
                }

                Rect innerRect = new Rect(windowRect.x + 10f, windowRect.y + 40f, UIConstants.WindowWidth - 20f, UIConstants.WindowHeight - 50f);

                var pawns = map.mapPawns.FreeColonistsSpawned;
                float lineHeight = 32f;
                float y = 0f;

                Widgets.BeginScrollView(innerRect, ref scrollPos, new Rect(0f, 0f, innerRect.width - 16f, pawns.Count() * lineHeight));
                foreach (var pawn in pawns)
                {
                    string activity = "Nothing";
                    if (pawn.CurJob != null)
                    {
                        activity = pawn.CurJob.GetReport(pawn);
                    }
                    else if (pawn.Downed)
                    {
                        activity = "Unconscious";
                    }
                    else if (pawn.Dead)
                    {
                        activity = "Dead";
                    }

                    string label = $"{pawn.LabelShortCap}: {activity}";
                    Rect lineRect = new Rect(0f, y, innerRect.width - 16f, lineHeight);
                    Widgets.Label(lineRect, label);
                    y += lineHeight;
                }
                Widgets.EndScrollView();
            }
        }
    }

}
