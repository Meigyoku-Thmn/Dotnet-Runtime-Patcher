﻿using Harmony;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace Launcher {
   using static Helper;
   using TranspilerTuple = ValueTuple<Type, string, Type[], string>;
   using CodeInstructions = IEnumerable<CodeInstruction>;
   using CodeInstructionMap = IDictionary<int, CodeInstructionWrapper>;
   public partial class Launcher {
      static List<TranspilerTuple> GetAllTranspilerConfigs() {
         var EDT = TargetAssembly.GetType("THMHJ.ED");
         var TitleT = TargetAssembly.GetType("THMHJ.Title");
         var EntranceT = TargetAssembly.GetType("THMHJ.Entrance");
         var BonusT = TargetAssembly.GetType("THMHJ.Bonus");
         var StageclearT = TargetAssembly.GetType("THMHJ.Stageclear");
         var GameT = TargetAssembly.GetType("THMHJ.Game");
         var SpriteFontXT = TargetAssembly.GetType("Microsoft.Xna.Framework.SpriteFontX");
         var SPECIALT = TargetAssembly.GetType("THMHJ.SPECIAL");
         var PLAYDATAT = TargetAssembly.GetType("THMHJ.PLAYDATA");
         var Texture2DT = XnaGraphicsAssembly.GetType("Microsoft.Xna.Framework.Graphics.Texture2D");
         var SpriteT = TargetAssembly.GetType("THMHJ.Sprite");
         var PlayDataT = TargetAssembly.GetType("THMHJ.PlayData");
         var RecordManagerT = TargetAssembly.GetType("THMHJ.RecordManager");
         var LongT = typeof(long);
         var IntT = typeof(int);
         var RecordSaveT = TargetAssembly.GetType("THMHJ.RecordSave");
         var ReplaySaveT = TargetAssembly.GetType("THMHJ.ReplaySave");
         var rs = new List<TranspilerTuple>() {
            // 5 lines of description in music room, align for more space
            (EntranceT, "Load", null, nameof(LoadMethodOfEntranceTranspiler)),
            // bless image fullscreen
            (GameT, "Init", null, nameof(InitMethodOfGameTranspiler)),
            // bless image fullscreen
            // 5 lines of description in music room, align for more space
            (EntranceT, "Draw", null, nameof(DrawMethodOfEntranceTranspiler)),
            // The MeasureString method is horrible for trailing space character (bigger width than real width)
            (SpriteFontXT, "addTex", null, nameof(AddTexTranspiler)),
            // Fix The Line Break Algorithm in Achievement Screen
            (SPECIALT, "Draw", null, nameof(DrawMethodOfSPECIALTranspiler)),
            // Make the character's portraits on top of ui frame
            (GameT, "SDraw", null, nameof(DrawMethodOfGameTranspiler)),
            // BossList.xna hack to enlarge canvases
            (BonusT, "Draw", null, nameof(DrawMethodOfBonus)),
            (StageclearT, "Draw", null, nameof(DrawMethodOfStageclear)),
            // Achivrate position and sourceRectangle
            (SPECIALT, "Texture", null, nameof(TextureMethodOfSPECIAL)),
            // Playdata position and sourceRectangle
            (PLAYDATAT, ".ctor", TypeL(Texture2DT, SpriteT, SpriteT, SpriteT, SpriteT), nameof(PLAYDATAConstructor)),
            // Translate 终 in Replay Saving Screen and Record Saving Screen
            (RecordSaveT, ".ctor", TypeL(PlayDataT, IntT, LongT), nameof(RecordSaveConstructor)),
            (ReplaySaveT, ".ctor", TypeL(RecordManagerT, IntT, LongT, IntT, IntT, IntT), nameof(ReplaySaveConstructor1)),
            (ReplaySaveT, ".ctor", TypeL(RecordManagerT, IntT, LongT, IntT, IntT, IntT, IntT), nameof(ReplaySaveConstructor2)),
            // Adjust stagetitle metrics
            (TitleT, "Draw", null, nameof(DrawMethodOfTitle)),
            // Adjust 31.xna metrics
            (EDT, "Draw", null, nameof(DrawMethodOfED)),
         };
         return rs;
      }
      static void SetupTranspiler() {
         var allTranspilerConfigs = GetAllTranspilerConfigs();
         foreach (var config in allTranspilerConfigs) {
            var original = config.Item2 != ".ctor" ?
               AccessTools.Method(config.Item1, config.Item2, config.Item3) :
               AccessTools.Constructor(config.Item1, config.Item3) as MethodBase;
            var transpiler = AccessTools.Method(typeof(Launcher), config.Item4);
            Harmony.Patch(original, null, null, new HarmonyMethod(transpiler));
         }
      }
      //=======================================================================
      static CodeInstructions DrawMethodOfED(CodeInstructions instructions, CodeInstructionMap instrByOffsets) {
         instrByOffsets[0x00b1].cdInstr.operand = 0f;
         instrByOffsets[0x00cb].cdInstr.operand = 640f;
         return instructions;
      }
      static CodeInstructions DrawMethodOfTitle(CodeInstructions instructions, CodeInstructionMap instrByOffsets) {
         var offsets = new int[] { 0x001a, 0x0045, 0x00fa, 0x0128 };
         foreach (var offset in offsets) {
            var instr = instrByOffsets[offset].cdInstr;
            instr.operand = 143; instr.opcode = OpCodes.Ldc_I4;
         }
         offsets = new int[] { 0x0103, 0x0131 };
         foreach (var offset in offsets) {
            var instr = instrByOffsets[offset].cdInstr;
            instr.operand = 50; instr.opcode = OpCodes.Ldc_I4;
         }
         return instructions;
      }
      static string EndString = "Xong";
      static CodeInstructions RecordSaveConstructor(CodeInstructions instructions, CodeInstructionMap instrByOffsets) {
         instrByOffsets[0x0242].cdInstr.operand = EndString;
         return instructions;
      }
      static CodeInstructions ReplaySaveConstructor1(CodeInstructions instructions, CodeInstructionMap instrByOffsets) {
         instrByOffsets[0x0242].cdInstr.operand = EndString;
         return instructions;
      }
      static CodeInstructions ReplaySaveConstructor2(CodeInstructions instructions, CodeInstructionMap instrByOffsets) {
         instrByOffsets[0x0242].cdInstr.operand = EndString;
         return instructions;
      }
      static CodeInstructions PLAYDATAConstructor(CodeInstructions instructions, CodeInstructionMap instrByOffsets) {
         instrByOffsets[0x0061].cdInstr.operand = 0f;
         return instructions;
      }
      static CodeInstructions TextureMethodOfSPECIAL(CodeInstructions instructions, CodeInstructionMap instrByOffsets) {
         instrByOffsets[0x00ac].cdInstr.operand = 0f;
         return instructions;
      }
      static CodeInstructions DrawMethodOfStageclear(CodeInstructions instructions, CodeInstructionMap instrByOffsets) {
         var FloatT = typeof(float);
         var Vector2T = XnaAssembly.GetType("Microsoft.Xna.Framework.Vector2");
         var Vector2C = AccessTools.Constructor(Vector2T, TypeL(FloatT, FloatT));
         var Texture2DT = XnaGraphicsAssembly.GetType("Microsoft.Xna.Framework.Graphics.Texture2D");
         var RectangleT = XnaAssembly.GetType("Microsoft.Xna.Framework.Rectangle");
         var NullableRectangleT = typeof(Nullable<>).MakeGenericType(RectangleT);
         var SpriteEffectsT = XnaGraphicsAssembly.GetType("Microsoft.Xna.Framework.Graphics.SpriteEffects");
         var ColorT = XnaAssembly.GetType("Microsoft.Xna.Framework.Color");
         var SBDrawM = AccessTools.Method(
            XnaGraphicsAssembly.GetType("Microsoft.Xna.Framework.Graphics.SpriteBatch"), "Draw",
            TypeL(Texture2DT, Vector2T, NullableRectangleT, ColorT, FloatT, Vector2T, FloatT, SpriteEffectsT, FloatT));
         var instrs = instructions as List<CodeInstruction>;
         {
            var bombTuple = new int[] { 8, 176, 121, 22 };
            var offsets = new int[] { 0x037c, 0x037d, 0x0382, 0x0384 };
            for (var i = 0; i < offsets.Length; i++) {
               var instr = instrByOffsets[offsets[i]].cdInstr;
               instr.operand = bombTuple[i];
               instr.opcode = OpCodes.Ldc_I4;
            }
            // position
            instrByOffsets[0x036d].cdInstr.operand = 219f; // x

            // origin
            var target = instrByOffsets[0x03aa];
            instrs.InsertRange(target.index, new CodeInstruction[] {
               new CodeInstruction(OpCodes.Ldc_R4, 0f),
               new CodeInstruction(OpCodes.Ldc_R4, (float)(bombTuple[2] - 1)),
               new CodeInstruction(OpCodes.Ldc_R4, 2f),
               new CodeInstruction(OpCodes.Newobj, Vector2C),
               new CodeInstruction(OpCodes.Ldc_R4, 1f),
               new CodeInstruction(OpCodes.Ldc_I4_0),
               new CodeInstruction(OpCodes.Ldc_R4, 0.0f),
            });
            target.cdInstr.operand = SBDrawM;
         }
         {
            var lostTuple = new int[] { 8, 154, 121, 22 };
            var offsets = new int[] { 0x0081, 0x0082, 0x0087, 0x0089 };
            for (var i = 0; i < offsets.Length; i++) {
               var instr = instrByOffsets[offsets[i]].cdInstr;
               instr.operand = lostTuple[i];
               instr.opcode = OpCodes.Ldc_I4;
            }
            // position
            instrByOffsets[0x0072].cdInstr.operand = 219f; // x

            // origin
            var target = instrByOffsets[0x00af];
            instrs.InsertRange(target.index, new CodeInstruction[] {
               new CodeInstruction(OpCodes.Ldc_R4, 0f),
               new CodeInstruction(OpCodes.Ldc_R4, (float)(lostTuple[2] - 1)),
               new CodeInstruction(OpCodes.Ldc_R4, 2f),
               new CodeInstruction(OpCodes.Newobj, Vector2C),
               new CodeInstruction(OpCodes.Ldc_R4, 1f),
               new CodeInstruction(OpCodes.Ldc_I4_0),
               new CodeInstruction(OpCodes.Ldc_R4, 0.0f),
            });
            target.cdInstr.operand = SBDrawM;
         }
         return instructions;
      }
      static CodeInstructions DrawMethodOfBonus(CodeInstructions instructions, CodeInstructionMap instrByOffsets) {
         var FloatT = typeof(float);
         var Vector2C = AccessTools.Constructor(
            XnaAssembly.GetType("Microsoft.Xna.Framework.Vector2"), TypeL(FloatT, FloatT));
         var instrs = instructions as List<CodeInstruction>;
         // sourceRectangle
         var timeTuple = new int[] { 228, 106, 137, 21 };
         var offsets = new int[] { 0x0222, 0x0227, 0x0229, 0x022b };
         for (var i = 0; i < offsets.Length; i++) {
            var instr = instrByOffsets[offsets[i]].cdInstr;
            instr.operand = timeTuple[i];
            instr.opcode = OpCodes.Ldc_I4;
         }
         // position
         instrByOffsets[0x0212].cdInstr.operand = 219f; // x
         // origin
         var target = instrByOffsets[0x0256];
         instrs.InsertRange(target.index + 1, new CodeInstruction[] {
            new CodeInstruction(OpCodes.Ldc_R4, (float)(timeTuple[2] - 1)),
            new CodeInstruction(OpCodes.Ldc_R4, 2f),
            new CodeInstruction(OpCodes.Newobj, Vector2C),
         });
         instrs.RemoveAt(target.index);
         return instrs;
      }
      static CodeInstructions InitMethodOfGameTranspiler(CodeInstructions instructions, CodeInstructionMap instrByOffsets) {
         instrByOffsets[0x000000b8].cdInstr.operand = 0f;
         instrByOffsets[0x000000b3].cdInstr.operand = 0f;
         return instructions;
      }
      static CodeInstructions LoadMethodOfEntranceTranspiler(CodeInstructions instructions, CodeInstructionMap instrByOffsets) {
         instrByOffsets[0x077d].cdInstr.opcode = OpCodes.Ldc_I4_5;
         instrByOffsets[0x07b2].cdInstr.opcode = OpCodes.Ldc_I4_5;
         instrByOffsets[0x07d0].cdInstr.opcode = OpCodes.Ldc_I4_5;
         instrByOffsets[0x07e0].cdInstr.opcode = OpCodes.Ldc_I4_5;
         return instructions;
      }
      static CodeInstructions DrawMethodOfEntranceTranspiler(CodeInstructions instructions, CodeInstructionMap instrByOffsets) {
         instrByOffsets[0x00000058].cdInstr.operand = 0f;
         instrByOffsets[0x00000053].cdInstr.operand = 0f;
         instrByOffsets[0x00000e11].cdInstr.opcode = OpCodes.Ldc_I4_5;
         return instructions;
      }
      static CodeInstructions AddTexTranspiler(CodeInstructions instructions, CodeInstructionMap instrByOffsets) {
         var myMeasureStringMethod = AccessTools.Method(typeof(Launcher), nameof(MeasureString));
         var targetInstr = instrByOffsets[0x0000002e];
         targetInstr.cdInstr.opcode = OpCodes.Call;
         targetInstr.cdInstr.operand = myMeasureStringMethod;
         return instructions;
      }
      // Graphics.MeasureString's accuracy sucks for space, why StarX hasn't noticed this, I think because it doesn't matter for chinese characters
      public static SizeF MeasureString(Graphics _this, string text, Font font, PointF origin, StringFormat stringFormat) {
         if (text[0] == ' ') text = " \u200D"; // ZERO-WIDTH JOINER SPACE, trick MeasureString into thinking that text has no trailing space
         return _this.MeasureString(text, font, origin, stringFormat);
      }
      static CodeInstructions DrawMethodOfSPECIALTranspiler(CodeInstructions instructions, CodeInstructionMap instrByOffsets) {
         var targetInstr = instrByOffsets[0x00000ee7];
         var ldloc_index = targetInstr.index;
         var str_local_index = ((LocalBuilder)targetInstr.cdInstr.operand).LocalIndex;
         var InsertLineBreakMethod = AccessTools.Method(typeof(Launcher), nameof(InsertLineBreak));
         var insertedInstructions = new List<CodeInstruction>() {
            new CodeInstruction(OpCodes.Ldloca, (short)str_local_index),
            new CodeInstruction(OpCodes.Call, InsertLineBreakMethod),
            new CodeInstruction(OpCodes.Ldstr, ""),
         };
         var instrs = instructions as List<CodeInstruction>;
         instrs.RemoveAt(ldloc_index);
         instrs.InsertRange(ldloc_index, insertedInstructions);
         return instrs;
      }
      public static void InsertLineBreak(ref string str) {
         // I just take this from the game itself
         var stringList = new List<string>();
         var source = new List<char>();
         var MainType = TargetAssembly.GetType("THMHJ.Main");
         dynamic dfont = AccessTools.Field(MainType, "dfont").GetValue(null);
         foreach (var ch in str) {
            source.Add(ch);
            if (dfont.MeasureString(source.ToArray()).X >= 450.0) {
               int num = source.LastIndexOf(' ');
               List<char> charList = num != -1 ? source.Take(num + 1).ToList() : source;
               stringList.Add(string.Join("", charList));
               source.RemoveRange(0, charList.Count);
            }
         }
         stringList.Add(string.Join<char>("", source));
         str = string.Join("\r\n", stringList);
      }
      /*
       * Summary of what this method do: it moves the ui frame drawing instructions to 2 places, right before the character portraits drawing instructions, and create a routine when the game is pause then insert the ui drawing instructions into it to keep the ui on screen, I will write more details in the future
       */
      static CodeInstructions DrawMethodOfGameTranspiler(CodeInstructions instructions, ILGenerator adderIL, CodeInstructionMap instrByOffsets) {
         var instrs = instructions as List<CodeInstruction>;
         var target1 = instrByOffsets[0x00000507];
         var target3 = instrByOffsets[0x000003b3];
         var target4 = instrByOffsets[0x000002ce];
         var target5 = instrByOffsets[0x00000385];

         var uiDrawIsts = instrs.Skip(target1.index).Take(7).Select(instr => instr.Clone()).ToList();
         var uiDrawIsts2 = uiDrawIsts.Select(instr => instr.Clone()).ToList();
         var grazeboxDrawIsts = instrs.Skip(target3.index).Take(10).Select(instr => instr.Clone()).ToList();
         uiDrawIsts.AddRange(grazeboxDrawIsts);

         target1.cdInstr.opcode = OpCodes.Nop;
         instrs.RemoveRange(target1.index + 1, 6);
         target3.cdInstr.opcode = OpCodes.Nop;
         instrs.RemoveRange(target3.index + 1, 9);
         var ifPauseIsts = instrs.Skip(target4.index).Take(3).Select(instr => instr.Clone()).ToList();
         ifPauseIsts[2].opcode = OpCodes.Brfalse;
         uiDrawIsts[0].labels = target5.cdInstr.labels;
         target5.cdInstr.labels = new List<Label>();
         instrs.InsertRange(target5.index, uiDrawIsts);
         ifPauseIsts[0].labels = target4.cdInstr.labels;
         target4.cdInstr.labels = new List<Label>();
         var label = adderIL.DefineLabel();
         ifPauseIsts[2].operand = label;
         instrs.InsertRange(target4.index, ifPauseIsts);
         instrs[target4.index + 3].labels.Add(label);
         instrs.InsertRange(target4.index + 3, uiDrawIsts2);
         return instrs;
      }
   }
}