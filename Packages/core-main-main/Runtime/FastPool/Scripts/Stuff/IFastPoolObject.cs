using UnityEngine;
using System.Collections;

namespace NabaGame.Core.Runtime.Pool
{
    public interface IFastPoolItem
    {
        void OnFastInstantiate();
        void OnFastDestroy();
    }
}