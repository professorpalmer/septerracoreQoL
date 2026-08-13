using System;
using System.Runtime.InteropServices;

namespace Septerra.Core.Hooks
{
    public interface IBattleCharacterSelector
    {
        public Int32? SelectedCharacterIndex { get; }

        public void QueueCharacterSelection(Int32 playerIndex);
        public void ResetSelectedCharacter();
        public Boolean HasPendingUpdateCharacterSelection();
    }

    public sealed unsafe class BattleCharacterSelector : IBattleCharacterSelector
    {
        private Int32 _pendingDesiredPlayerSelectionIndex = -1;
        
        public Int32? SelectedCharacterIndex
        {
            get
            {
                Int32 selectedCharacterIndex = SelectedPlayerIndex;
                Log.Message($"[SelectedCharacterIndex]: {selectedCharacterIndex}");
                if (selectedCharacterIndex < 0)
                    return null;

                return selectedCharacterIndex;
            }
        }

        public void QueueCharacterSelection(Int32 playerIndex)
        {
            if (playerIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(playerIndex));

            _pendingDesiredPlayerSelectionIndex = playerIndex;
            // NextPlayerSelectionIndex = playerIndex;
            // DesiredPlayerSelectionIndex = playerIndex;
            Log.Message($"[QueueCharacterSelection] PlayerIndex: {playerIndex}");
        }

        public void ResetSelectedCharacter()
        {
            if (SelectedPlayerIndex < 0)
                return;
            
            Log.Message($"[ResetSelectedCharacter]");
            
            //NextPlayerSelectionIndex = -2;
            DesiredPlayerSelectionIndex = -1;
            SelectedPlayerIndex = -1;
            
            sub_407B70(1);
            dword_0x4A2968 = -1;
        }
        
        public Boolean HasPendingUpdateCharacterSelection()
        {
            if (DesiredPlayerSelectionIndex >= 0)
            {
                if (_pendingDesiredPlayerSelectionIndex >= 0)
                    Log.Message($"[HasPendingUpdateCharacterSelection] Ignored: {_pendingDesiredPlayerSelectionIndex}");

                _pendingDesiredPlayerSelectionIndex = -1;
                return false;
            }
            
            Int32 pending = _pendingDesiredPlayerSelectionIndex;
            if (pending < 0)
                return false;

            Log.Message($"[HasPendingUpdateCharacterSelection] Yes: {_pendingDesiredPlayerSelectionIndex}");

            Int32 hotKet = _pendingDesiredPlayerSelectionIndex switch
            {
                0 => 0x51,
                1 => 0x41,
                2 => 0x5A,
                _ => 0
            };

            _pendingDesiredPlayerSelectionIndex = -1;
            ProcessHotKey(hotKet);
            // DesiredPlayerSelectionIndex = _pendingDesiredPlayerSelectionIndex;
            //QueueCharacterSelection(pending);
            return true;
        }

        /// <summary>
        /// The value will be used on the next iteration of WinMain to pick available Player Character.
        /// Default value: -1
        /// Valid values: from 0 to party size
        /// </summary>
        private static Int32 DesiredPlayerSelectionIndex
        {
            get => *((Int32*) Main.AddressTable.Setperra_Global_DesiredActorToSelect);
            set => *((Int32*) Main.AddressTable.Setperra_Global_DesiredActorToSelect) = value;
        }

        /// <summary>
        /// The value will be used on the next iteration of WinMain to pick Player Character without any checks.
        /// Will be ignored if DesiredPlayerSelectionIndex is set.
        /// Default value: -2
        /// Valid values: from -1 to party size
        /// </summary>
        private static Int32 NextPlayerSelectionIndex
        {
            get => *((Int32*) Main.AddressTable.Setperra_Global_SelectedCharacterInBattle);
            set => *((Int32*) Main.AddressTable.Setperra_Global_SelectedCharacterInBattle) = value;
        }

        /// <summary>
        /// Currently selected player character index.
        /// </summary>
        private static Int32 SelectedPlayerIndex
        {
            get => *((Int32*) Main.AddressTable.Setperra_Global_SelectedPlayerActor);
            set => *((Int32*) Main.AddressTable.Setperra_Global_SelectedPlayerActor) = value;
        }
        
        private static Int32 dword_0x4A2968
        {
            get => *((Int32*) Main.AddressTable.Setperra_Global_Dword_0x4A2968);
            set => *((Int32*) Main.AddressTable.Setperra_Global_Dword_0x4A2968) = value;
        }
        
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate SceneInstance* sub_407B70_(Int32 value);
        private static readonly sub_407B70_ sub_407B70 = Marshal.GetDelegateForFunctionPointer<sub_407B70_>(new IntPtr(Main.AddressTable.Setperra_Cdecl_Function_407B70));
        
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate Int32 ProcessHotKey_(Int32 value);
        private static readonly ProcessHotKey_ ProcessHotKey = Marshal.GetDelegateForFunctionPointer<ProcessHotKey_>(new IntPtr(Main.AddressTable.Septerra_Common_ProcessHotKey));
    }
    
    public sealed unsafe class BattleDispatcher
    {
        public IBattleCharacterSelector CharacterSelector { get; } = new BattleCharacterSelector();

        public Boolean Dispatch()
        {
            if (IsAtbDisabled != 0)
                return false;

            if (CharacterSelector.HasPendingUpdateCharacterSelection())
                return true;
            
            if (!HotKeyManager.TryEvict(HotKey.NextTurn))
                return false;

            UnsafeArray<SC_CombatListEntry> playerCombatants = PlayerCombatants;
            if (playerCombatants.Count < 1)
                return false;
            
            UnsafeArray<SC_CombatListEntry> enemyCombatants = EnemyCombatants;
            if (enemyCombatants.Count < 1)
                return false;
            
            UnsafeArray<SC_CombatListEntry> alliedCombatants = AlliedCombatants;

                Boolean hasChanged = false;
                Boolean hasNext = false;
                Boolean partyGainedBar = false;
                
                Int32 disabledParty = 0;
                Int32 disabledEnemies = 0;

                while (true)
                {
                    partyGainedBar = false;
                    disabledParty = 0;
                    disabledEnemies = 0;
                    hasNext = false;

                    for (Int32 i = 0; i < playerCombatants.Count; i++)
                    {
                        SC_CombatListEntry* player = playerCombatants[i];
                        ActorInfo2* playerActor = player->Actor;
                        Boolean isDisabled = IsActorDisabled(playerActor);
                        if (isDisabled)
                            disabledParty++;
                        
                        Int32 previousAtb = playerActor->Battle.ATB / 3333;
                        IncreaseActorBattleTime(playerActor);
                        ClampActorAtbToNextBar(playerActor, previousAtb);
                        Int32 currentAtb = playerActor->Battle.ATB / 3333;

                        if (previousAtb != currentAtb)
                        {
                            hasChanged = true;
                            partyGainedBar = true;
                            CharacterSelector.ResetSelectedCharacter();
                            CharacterSelector.QueueCharacterSelection(i);
                        }

                        if (!isDisabled && currentAtb < 3)
                            hasNext = true;
                    }

                    // One F press fills at most one ATB bar for the party. Do not also
                    // tick enemies/allies on the completing step — that is how F used
                    // to skip 1->3 and let an enemy act in the same skip.
                    if (partyGainedBar)
                        break;
                
                    for (Int32 i = 0; i < enemyCombatants.Count; i++)
                    {
                        SC_CombatListEntry* enemy = enemyCombatants[i];
                        ActorInfo2* enemyActor = enemy->Actor;
                        Boolean isDisabled = IsActorDisabled(enemyActor);
                        if (isDisabled)
                        {
                            disabledEnemies++;
                            continue;
                        }

                        Int32 previousAtb = enemyActor->Battle.ATB / 3333;
                        IncreaseActorBattleTime(enemyActor);
                        Int32 currentAtb = enemyActor->Battle.ATB / 3333;

                        if (previousAtb != currentAtb)
                        {
                            hasChanged = true;
                            ReselectCurrentPlayerCharacter();
                        }
                    }
                
                    for (Int32 i = 0; i < alliedCombatants.Count; i++)
                    {
                        SC_CombatListEntry* ally = alliedCombatants[i];
                        ActorInfo2* allyActor = ally->Actor;
                        Int32 previousAtb = allyActor->Battle.ATB / 3333;
                        IncreaseActorBattleTime(allyActor);
                        Int32 currentAtb = allyActor->Battle.ATB / 3333;
                    
                        if (previousAtb != currentAtb)
                        {
                            hasChanged = true;
                            ReselectCurrentPlayerCharacter();
                        }
                    }
                    
                    if (hasChanged || !hasNext)
                        break;
                    
                    if (disabledParty == playerCombatants.Count || disabledEnemies == enemyCombatants.Count)
                        break;
                }

            return false;
        }

        private void ReselectCurrentPlayerCharacter()
        {
            Int32? currentPlayerIndex = CharacterSelector.SelectedCharacterIndex;
            if (currentPlayerIndex != null)
            {
                CharacterSelector.ResetSelectedCharacter();
                CharacterSelector.QueueCharacterSelection(currentPlayerIndex.Value);
            }
        }

        private static void ClampActorAtbToNextBar(ActorInfo2* actor, Int32 previousBarCount)
        {
            Int32 currentBarCount = actor->Battle.ATB / 3333;
            if (currentBarCount <= previousBarCount + 1)
                return;

            Int32 clamped = (previousBarCount + 1) * 3333;
            if (clamped > Int16.MaxValue)
                clamped = Int16.MaxValue;
            actor->Battle.ATB = (Int16)clamped;
        }

        private static Boolean IsActorDisabled(ActorInfo2* playerActor)
        {
            var f1 = playerActor->Battle.SomeFlags;
            var f2 = playerActor->SomeFlags;
            var isDisabled = (f1 & 0x10000) == 0 || (f2 & 0x8) == 0;
            return isDisabled;
        }

        private static UnsafeArray<SC_CombatListEntry> PlayerCombatants => new((SC_CombatListEntry*)Main.AddressTable.Setperra_Global_ArrayOf3CombatEntries, PlayerCombatantCount);
        private static UnsafeArray<SC_CombatListEntry> EnemyCombatants => new((SC_CombatListEntry*)Main.AddressTable.Setperra_Global_EnemiesCombatList, EnemyCombatantCount);
        private static UnsafeArray<SC_CombatListEntry> AlliedCombatants => new((SC_CombatListEntry*)Main.AddressTable.Setperra_Global_AlliesCombatList, AlliedCombatantCount);
        
        private static Int32 PlayerCombatantCount
        {
            get => *((Int32*) Main.AddressTable.Setperra_Global_BattlePartySize);
            set => *((Int32*) Main.AddressTable.Setperra_Global_BattlePartySize) = value;
        }

        private static Int32 EnemyCombatantCount
        {
            get => *((Int32*) Main.AddressTable.Setperra_Global_EnemiesCombatListCount);
            set => *((Int32*) Main.AddressTable.Setperra_Global_EnemiesCombatListCount) = value;
        }

        private static Int32 AlliedCombatantCount
        {
            get => *((Int32*) Main.AddressTable.Setperra_Global_AlliesCombatListCount);
            set => *((Int32*) Main.AddressTable.Setperra_Global_AlliesCombatListCount) = value;
        }
        
        private static Int32 IsAtbDisabled
        {
            get => *((Int32*) Main.AddressTable.Setperra_Global_IsAtbDisabled);
            set => *((Int32*) Main.AddressTable.Setperra_Global_IsAtbDisabled) = value;
        }
        
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate Int32 IncreaseActorBattleTime_(ActorInfo2* a1);
        private static readonly IncreaseActorBattleTime_ IncreaseActorBattleTime = Marshal.GetDelegateForFunctionPointer<IncreaseActorBattleTime_>(new IntPtr(Main.AddressTable.Setperra_Cdecl_Function_IncreaseActorBattleTime));
    }
    
    public sealed unsafe class BattleDispatcher2
    {
        private static UnsafeArray<SC_CombatListEntry> PlayerCombatants => new((SC_CombatListEntry*)Main.AddressTable.Setperra_Global_ArrayOf3CombatEntries, PlayerCombatantCount);
        private static UnsafeArray<SC_CombatListEntry> EnemyCombatants => new((SC_CombatListEntry*)Main.AddressTable.Setperra_Global_EnemiesCombatList, EnemyCombatantCount);
        private static UnsafeArray<SC_CombatListEntry> AlliesCombatants => new((SC_CombatListEntry*)Main.AddressTable.Setperra_Global_AlliesCombatList, AlliesCombatantCount);

        private static Int32 dword_0x4A2968
        {
            get => *((Int32*) Main.AddressTable.Setperra_Global_Dword_0x4A2968);
            set => *((Int32*) Main.AddressTable.Setperra_Global_Dword_0x4A2968) = value;
        }

        private static UInt32 SelectedCharacterInBattle
        {
            get => *((UInt32*) Main.AddressTable.Setperra_Global_SelectedCharacterInBattle);
            set => *((UInt32*) Main.AddressTable.Setperra_Global_SelectedCharacterInBattle) = value;
        }
        
        private static Int32 PlayerCombatantCount
        {
            get => *((Int32*) Main.AddressTable.Setperra_Global_BattlePartySize);
            set => *((Int32*) Main.AddressTable.Setperra_Global_BattlePartySize) = value;
        }

        private static Int32 EnemyCombatantCount
        {
            get => *((Int32*) Main.AddressTable.Setperra_Global_EnemiesCombatListCount);
            set => *((Int32*) Main.AddressTable.Setperra_Global_EnemiesCombatListCount) = value;
        }

        private static Int32 AlliesCombatantCount
        {
            get => *((Int32*) Main.AddressTable.Setperra_Global_AlliesCombatListCount);
            set => *((Int32*) Main.AddressTable.Setperra_Global_AlliesCombatListCount) = value;
        }

        private static Int32 IsAtbDisabled
        {
            get => *((Int32*) Main.AddressTable.Setperra_Global_IsAtbDisabled);
            set => *((Int32*) Main.AddressTable.Setperra_Global_IsAtbDisabled) = value;
        }

        private static Int32 DesiredActorToSelect
        {
            get => *((Int32*) Main.AddressTable.Setperra_Global_DesiredActorToSelect);
            set => *((Int32*) Main.AddressTable.Setperra_Global_DesiredActorToSelect) = value;
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate SceneInstance* sub_407B70_(Int32 value);
        private static readonly sub_407B70_ sub_407B70 = Marshal.GetDelegateForFunctionPointer<sub_407B70_>(new IntPtr(Main.AddressTable.Setperra_Cdecl_Function_407B70));

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate Int32 IncreaseActorBattleTime_(ActorInfo2* a1);
        private static readonly IncreaseActorBattleTime_ IncreaseActorBattleTime = Marshal.GetDelegateForFunctionPointer<IncreaseActorBattleTime_>(new IntPtr(Main.AddressTable.Setperra_Cdecl_Function_IncreaseActorBattleTime));

        private Int32 _selectAgainPlayerIndex = -1;
        private Boolean _forceDispatch = false;
        private Boolean _playerGotTurn = false;
        private Boolean _allPlayersIsFull = false;
        private Int32 _minEnemyAtb = 0;
        private Boolean _isEnemyMoved;

        public unsafe Boolean Dispatch()
        {
            if (_selectAgainPlayerIndex > -1)
            {
                DesiredActorToSelect = _selectAgainPlayerIndex;
                return false;
            }

            if (!HotKeyManager.TryEvict(HotKey.NextTurn) && !_forceDispatch)
                return false;

            _selectAgainPlayerIndex = -1;
            _forceDispatch = false;
            _playerGotTurn = false;

            if (IsAtbDisabled != 0)
                return false;

            var partySize = PlayerCombatantCount;
            if (partySize < 1)
                return false;

            var enemySize = EnemyCombatantCount;
            if (enemySize < 1)
                return false;

            var otherSize = AlliesCombatantCount;

            Boolean hasNext = false;
            Boolean hasChanged = false;

            do
            {
                if (!TryUpdatePlayerTimers(partySize, ref hasNext, ref hasChanged))
                    return false;

                if (!TryUpdateEnemyTimers(enemySize, ref hasNext, ref hasChanged))
                    return false;

                TryUpdateOtherTimers(otherSize, ref hasChanged);
                
            } while (hasNext && !hasChanged);

            return false;
        }

        private Boolean TryUpdatePlayerTimers(Int32 partySize, ref Boolean hasNext, ref Boolean hasChanged)
        {
            _allPlayersIsFull = true;
            
            var disabledParty = 0;
            var maxAtb = 0;

            for (var i = 0; i < partySize; i++)
            {
                SC_CombatListEntry* playerCombatant = PlayerCombatants[i];

                var playerActor = playerCombatant->Actor;
                var isDisabled = IsActorDisabled(playerActor);

                var previousAtb = playerActor->Battle.ATB / 3333;
                if (isDisabled)
                {
                    disabledParty++;
                }
                else if (previousAtb < 3)
                {
                    _allPlayersIsFull = false;
                }

                IncreaseActorBattleTime(playerActor);

                if (isDisabled)
                    continue;

                var currentAtb = playerActor->Battle.ATB / 3333;
                if (currentAtb != 3)
                    hasNext = true;

                if (currentAtb > maxAtb)
                {
                    maxAtb = currentAtb;
                    // if (!_playerGotTurn && gSelectedPlayerActor == -1)
                    //     DesiredActorToSelect = i;
                }
                
                if (previousAtb != currentAtb)
                {
                    DesiredActorToSelect = i;
                    hasChanged = true;
                    _playerGotTurn = true;
                    ReselectCurrentPlayer();
                }
            }

            if (disabledParty == partySize)
                return false;
            return true;
        }

        private Boolean TryUpdateEnemyTimers(Int32 enemySize, ref Boolean hasNext, ref Boolean hasChanged)
        {
            var minAtb = Int32.MaxValue;

            var disabledEnemy = 0;
            for (Int32 i = 0; i < enemySize; i++)
            {
                SC_CombatListEntry* enemyCombatant = EnemyCombatants[i];

                if (!IsActorDisabled(enemyCombatant->Actor))
                {
                    var atb = enemyCombatant->Actor->Battle.ATB;
                    if (atb < minAtb)
                        minAtb = atb;
                    
                    var beforeAtb = atb / 3333;
                    IncreaseActorBattleTime(enemyCombatant->Actor);
                    var currentAtb = enemyCombatant->Actor->Battle.ATB / 3333;
                    if (currentAtb != 3 && _allPlayersIsFull)
                        hasNext = true;

                    if (beforeAtb != currentAtb)
                    {
                        ReselectCurrentPlayer();
                        hasChanged = true;
                    }
                }
                else
                {
                    disabledEnemy++;
                }
            }

            if (minAtb < _minEnemyAtb)
                _isEnemyMoved = true;

            _minEnemyAtb = minAtb;

            if (disabledEnemy == enemySize)
                return false;
            return true;
        }

        private void ReselectCurrentPlayer()
        {
            // if (DesiredActorToSelect == -1)
            //     DesiredActorToSelect = gSelectedPlayerActor;
            // if (DesiredActorToSelect == -1)
            //     return;
            //
            // _forceDispatch = !_playerGotTurn;
            //
            // _selectAgainPlayerIndex = DesiredActorToSelect;
            // SelectedCharacterInBattle = 0xFFFFFFFF;
            // sub_407B70(1);
            // dword_0x4A2968 = -1;
            // gSelectedPlayerActor = -1;
            // DesiredActorToSelect = -1;
        }

        private void TryUpdateOtherTimers(Int32 otherSize, ref Boolean hasChanged)
        {
            for (Int32 i = 0; i < otherSize; i++)
            {
                var other = AlliesCombatants[i];
                var atb = other->Actor->Battle.ATB;
                var beforeAtb = atb / 3333;
                IncreaseActorBattleTime(other->Actor);
                
                var currentAtb = other->Actor->Battle.ATB / 3333;
                if (beforeAtb != currentAtb)
                {
                    ReselectCurrentPlayer();
                    hasChanged = true;
                }
            }
        }

        private static Boolean IsActorDisabled(ActorInfo2* playerActor)
        {
            var f1 = playerActor->Battle.SomeFlags;
            var f2 = playerActor->SomeFlags;
            var isDisabled = (f1 & 0x10000) == 0 || (f2 & 0x8) == 0;
            return isDisabled;
        }
    }
}