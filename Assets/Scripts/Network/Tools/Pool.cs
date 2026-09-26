using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Pool<T> where T : class
{
    private readonly Queue<T> pool = new();
    private event Func<T> generator;
    private event Action<T> OnGet;
    private event Action<T> OnReturn;
    public int count => pool.Count;

    public Pool(Func<T> generator, int capacity = 4, Action<T> OnGet = null, Action<T> OnReturn = null)
    {
        this.generator = generator;
        this.OnGet = OnGet;
        this.OnReturn = OnReturn;
        for (int i = 0; i < capacity; i++)
            pool.Enqueue(this.generator());
    }
    public T Get()
    {
        T result = pool.Count > 0 ? pool.Dequeue() : generator();
        OnGet?.Invoke(result);
        return result;
    }

    public void Return(T item)
    {
        if (item == null)
            return;
        pool.Enqueue(item);
        OnReturn?.Invoke(item);
    }
}
