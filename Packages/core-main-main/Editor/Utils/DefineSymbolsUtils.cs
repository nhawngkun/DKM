using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace NabaGame.Core.Editor.Utils
{
    [InitializeOnLoad]
    public class DefineSymbolsUtils : MonoBehaviour
    {
        [MenuItem("BMH Game/Add Define Symbols/Add 'MODULE'", false, 0)]
        private static void AddModule()
        {
            NB_GlobalDefineUtils.AddDefine("BMH_APPLOVIN_MAX");
            NB_GlobalDefineUtils.AddDefine("BMH_FIREBASE_ANALYTIC");
            NB_GlobalDefineUtils.AddDefine("BMH_FIREBASE_REMOTE");
            NB_GlobalDefineUtils.AddDefine("BMH_APP_REVIEW");
            NB_GlobalDefineUtils.AddDefine("BMH_NOTIFICATION");
            NB_GlobalDefineUtils.AddDefine("BMH_SINGULAR");
        }

        [MenuItem("BMH Game/Add Define Symbols/Add 'RELEASE'", false, 50)]
        private static void RELEASE()
        {
            NB_GlobalDefineUtils.AddDefine("RELEASE");
        }

        [MenuItem("BMH Game/Remove Define Symbols/Remove 'RELEASE'", false, 50)]
        private static void RM_RELEASE()
        {
            NB_GlobalDefineUtils.RemoveDefine("RELEASE");
        }

        [MenuItem("BMH Game/Marketing Panel", false)]
        private static void UA_UI()
        {
            UAEditor.OpenWindow();
        }
    }
}