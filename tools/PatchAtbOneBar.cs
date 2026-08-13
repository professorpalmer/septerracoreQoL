using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

internal static class PatchAtbOneBar
{
    private static int Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: PatchAtbOneBar <in.dll> <out.dll>");
            return 2;
        }

        string inputPath = args[0];
        string outputPath = args[1];

        using (AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(inputPath, new ReaderParameters { ReadWrite = false }))
        {
            TypeDefinition dispatcher = assembly.MainModule.GetType("Septerra.Core.Hooks.BattleDispatcher");
            if (dispatcher == null)
            {
                Console.Error.WriteLine("BattleDispatcher type not found.");
                return 1;
            }

            PatchDispatch(dispatcher);
            PatchPlayerTimers(dispatcher);

            string dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            assembly.Write(outputPath);
        }

        Console.WriteLine("Patched " + outputPath);
        return 0;
    }

    private static void PatchDispatch(TypeDefinition dispatcher)
    {
        MethodDefinition method = dispatcher.Methods.First(m => m.Name == "Dispatch" && !m.IsStatic);
        ILProcessor il = method.Body.GetILProcessor();
        ModuleDefinition module = method.Module;

        Instruction tryEvict = method.Body.Instructions.First(i =>
            i.OpCode == OpCodes.Call &&
            i.Operand is MethodReference &&
            ((MethodReference)i.Operand).Name == "TryEvict");
        Instruction evictBrtrue = tryEvict.Next;
        if (evictBrtrue == null || (evictBrtrue.OpCode != OpCodes.Brtrue && evictBrtrue.OpCode != OpCodes.Brtrue_S))
            throw new InvalidOperationException("Expected brtrue after TryEvict.");

        Instruction handle = (Instruction)evictBrtrue.Operand;
        Instruction forceBrtrue = null;
        for (Instruction i = evictBrtrue.Next; i != null && i != handle; i = i.Next)
        {
            if ((i.OpCode == OpCodes.Brtrue || i.OpCode == OpCodes.Brtrue_S) && i.Operand == handle)
                forceBrtrue = i;
        }
        if (forceBrtrue == null)
            throw new InvalidOperationException("Expected _forceDispatch brtrue into the F handle block.");

        VariableDefinition fromF = new VariableDefinition(module.TypeSystem.Boolean);
        method.Body.Variables.Add(fromF);

        Instruction setF = il.Create(OpCodes.Ldc_I4_1);
        Instruction stF = il.Create(OpCodes.Stloc_S, fromF);
        Instruction brHandle = il.Create(OpCodes.Br, handle);
        Instruction setForce = il.Create(OpCodes.Ldc_I4_0);
        Instruction stForce = il.Create(OpCodes.Stloc_S, fromF);
        il.InsertBefore(handle, setF);
        il.InsertBefore(handle, stF);
        il.InsertBefore(handle, brHandle);
        il.InsertBefore(handle, setForce);
        il.InsertBefore(handle, stForce);
        evictBrtrue.Operand = setF;
        forceBrtrue.Operand = setForce;

        Instruction finalRet = method.Body.Instructions.Last(i => i.OpCode == OpCodes.Ret);
        Instruction finalLdc = finalRet.Previous;
        if (finalLdc == null || finalLdc.OpCode != OpCodes.Ldc_I4_0)
            throw new InvalidOperationException("Expected final ldc.i4.0 ret.");
        finalLdc.OpCode = OpCodes.Ldloc_S;
        finalLdc.Operand = fromF;
        Console.WriteLine("Dispatch: F press returns true so vanilla ATB does not also tick.");

        Instruction callPlayers = method.Body.Instructions.First(i =>
            i.OpCode == OpCodes.Call &&
            i.Operand is MethodReference &&
            ((MethodReference)i.Operand).Name == "TryUpdatePlayerTimers");
        Instruction playersBrtrue = callPlayers.Next;
        if (playersBrtrue == null || (playersBrtrue.OpCode != OpCodes.Brtrue && playersBrtrue.OpCode != OpCodes.Brtrue_S))
            throw new InvalidOperationException("Expected brtrue after TryUpdatePlayerTimers.");

        Instruction otherCall = method.Body.Instructions.First(i =>
            i.OpCode == OpCodes.Call &&
            i.Operand is MethodReference &&
            ((MethodReference)i.Operand).Name == "TryUpdateOtherTimers");
        Instruction loopCheck = otherCall.Next;
        if (loopCheck == null || loopCheck.OpCode != OpCodes.Ldloc_3)
            throw new InvalidOperationException("Expected ldloc.3 loop check after ally timer update.");

        Instruction failRet = playersBrtrue.Next != null ? playersBrtrue.Next.Next : null;
        if (failRet == null || failRet.OpCode != OpCodes.Ret)
            throw new InvalidOperationException("Missing ret after failed player timer update.");
        Instruction enemyBlock = failRet.Next;

        Instruction skipCheck = il.Create(OpCodes.Ldloc_S, fromF);
        Instruction skipBr = il.Create(OpCodes.Brtrue, loopCheck);
        il.InsertBefore(enemyBlock, skipCheck);
        il.InsertBefore(enemyBlock, skipBr);
        playersBrtrue.Operand = skipCheck;
        Console.WriteLine("Dispatch: F path ticks party only; skip enemy/ally ATB on the same press.");
    }

    private static void PatchPlayerTimers(TypeDefinition dispatcher)
    {
        MethodDefinition method = dispatcher.Methods.First(m => m.Name == "TryUpdatePlayerTimers");
        ILProcessor il = method.Body.GetILProcessor();

        FieldReference battleField = null;
        FieldReference atbField = null;
        foreach (Instruction i in method.Body.Instructions)
        {
            if (i.OpCode == OpCodes.Ldflda && i.Operand is FieldReference && ((FieldReference)i.Operand).Name == "Battle")
                battleField = (FieldReference)i.Operand;
            if (i.OpCode == OpCodes.Ldfld && i.Operand is FieldReference && ((FieldReference)i.Operand).Name == "ATB")
                atbField = (FieldReference)i.Operand;
        }
        if (battleField == null || atbField == null)
            throw new InvalidOperationException("Could not find Battle.ATB fields.");

        // Second store to V_5 is current bar count after IncreaseActorBattleTime.
        Instruction[] stores = method.Body.Instructions.Where(i =>
            (i.OpCode == OpCodes.Stloc_S || i.OpCode == OpCodes.Stloc) &&
            i.Operand is VariableDefinition &&
            ((VariableDefinition)i.Operand).Index == 5).ToArray();
        if (stores.Length < 1)
            throw new InvalidOperationException("Could not find current-bar stloc V_5.");

        Instruction stCurrentBar = stores[stores.Length - 1];
        VariableDefinition previousBar = method.Body.Variables[4];
        VariableDefinition currentBar = method.Body.Variables[5];
        Instruction afterClamp = stCurrentBar.Next;

        Instruction[] clampBody =
        {
            il.Create(OpCodes.Ldloc_3),
            il.Create(OpCodes.Ldflda, battleField),
            il.Create(OpCodes.Ldloc_S, previousBar),
            il.Create(OpCodes.Ldc_I4_1),
            il.Create(OpCodes.Add),
            il.Create(OpCodes.Ldc_I4, 3333),
            il.Create(OpCodes.Mul),
            il.Create(OpCodes.Conv_I2),
            il.Create(OpCodes.Stfld, atbField),
            il.Create(OpCodes.Ldloc_S, previousBar),
            il.Create(OpCodes.Ldc_I4_1),
            il.Create(OpCodes.Add),
            il.Create(OpCodes.Stloc_S, currentBar)
        };
        foreach (Instruction i in clampBody)
            il.InsertBefore(afterClamp, i);

        Instruction ble = il.Create(OpCodes.Ble_S, afterClamp);
        Instruction[] compare =
        {
            il.Create(OpCodes.Ldloc_S, currentBar),
            il.Create(OpCodes.Ldloc_S, previousBar),
            il.Create(OpCodes.Ldc_I4_1),
            il.Create(OpCodes.Add),
            ble
        };
        foreach (Instruction i in compare)
            il.InsertBefore(clampBody[0], i);

        Console.WriteLine("TryUpdatePlayerTimers: clamp ATB to at most +1 bar per F tick.");
    }
}
