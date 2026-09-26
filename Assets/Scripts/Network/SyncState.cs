using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SyncState<T>
    where T : new()
{
    private NetBehaviour behaviour;
    private int stateIndex;
    private T _value;
    public T value
    {
        get => _value;
        set
        {
            T oldValue = _value;
            _value = value;

            bool isEqual = EqualityComparer<T>.Default.Equals(oldValue, value);
            if (isEqual == false)
            {
                SetDirty();
                OnValueChange?.Invoke(oldValue, value);
            }
        }
    }
    public event Action<T, T> OnValueChange;
    public SyncState(NetBehaviour netBehaviour, Action<T, T> action)
    {
        _value = new();
        this.behaviour = netBehaviour;
        OnValueChange = action;
    }

    private void SetDirty()
        => behaviour.SetDirty(1UL << stateIndex);
}
