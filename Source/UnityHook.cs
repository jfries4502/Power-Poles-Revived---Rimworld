using System;
using UnityEngine;

namespace RimForge
{
    public class UnityHook : MonoBehaviour
    {
        public static event Action UponApplicationQuit;

        private void Awake()
        {
            DontDestroyOnLoad(this.gameObject);
        }

        private void OnApplicationQuit()
        {
            Core.Log("Detected application quit...");
            UponApplicationQuit?.Invoke();
        }
    }
}
