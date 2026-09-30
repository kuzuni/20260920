using System;

using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;

namespace BACKND.Database
{
    public static class DatabaseLoop
    {
        internal enum AddMode { Beginning, End }

        public static Action OnEarlyUpdate;
        public static Action OnLateUpdate;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ResetStatics()
        {
            OnEarlyUpdate = null;
            OnLateUpdate = null;
        }

        internal static int FindPlayerLoopEntryIndex(PlayerLoopSystem.UpdateFunction function, PlayerLoopSystem playerLoop, Type playerLoopSystemType)
        {
            if (playerLoop.type == playerLoopSystemType)
                return Array.FindIndex(playerLoop.subSystemList, (elem => elem.updateDelegate == function));

            if (playerLoop.subSystemList != null)
            {
                for (int i = 0; i < playerLoop.subSystemList.Length; ++i)
                {
                    int index = FindPlayerLoopEntryIndex(function, playerLoop.subSystemList[i], playerLoopSystemType);
                    if (index != -1) return index;
                }
            }
            return -1;
        }

        internal static bool AddToPlayerLoop(PlayerLoopSystem.UpdateFunction function, Type ownerType, ref PlayerLoopSystem playerLoop, Type playerLoopSystemType, AddMode addMode)
        {
            if (playerLoop.type == playerLoopSystemType)
            {
                if (Array.FindIndex(playerLoop.subSystemList, (s => s.updateDelegate == function)) != -1)
                {
                    return true;
                }

                int oldListLength = (playerLoop.subSystemList != null) ? playerLoop.subSystemList.Length : 0;
                Array.Resize(ref playerLoop.subSystemList, oldListLength + 1);

                PlayerLoopSystem system = new PlayerLoopSystem
                {
                    type = ownerType,
                    updateDelegate = function
                };

                if (addMode == AddMode.Beginning)
                {
                    Array.Copy(playerLoop.subSystemList, 0, playerLoop.subSystemList, 1, playerLoop.subSystemList.Length - 1);
                    playerLoop.subSystemList[0] = system;
                }
                else if (addMode == AddMode.End)
                {
                    playerLoop.subSystemList[oldListLength] = system;
                }

                return true;
            }

            if (playerLoop.subSystemList != null)
            {
                for (int i = 0; i < playerLoop.subSystemList.Length; ++i)
                {
                    if (AddToPlayerLoop(function, ownerType, ref playerLoop.subSystemList[i], playerLoopSystemType, addMode))
                        return true;
                }
            }
            return false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void RuntimeInitializeOnLoad()
        {
            PlayerLoopSystem playerLoop = PlayerLoop.GetCurrentPlayerLoop();

            bool earlyAdded = AddToPlayerLoop(DatabaseEarlyUpdate, typeof(DatabaseLoop), ref playerLoop, typeof(EarlyUpdate), AddMode.End);
            bool lateAdded = AddToPlayerLoop(DatabaseLateUpdate, typeof(DatabaseLoop), ref playerLoop, typeof(PreLateUpdate), AddMode.End);

            PlayerLoop.SetPlayerLoop(playerLoop);

        }

        static void DatabaseEarlyUpdate()
        {
            if (!Application.isPlaying) return;

            OnEarlyUpdate?.Invoke();
        }

        static void DatabaseLateUpdate()
        {
            if (!Application.isPlaying) return;

            if (OnLateUpdate != null)
            {
                OnLateUpdate.Invoke();
            }
        }
    }
}