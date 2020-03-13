using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ConcurrentQueue<TValue> : Queue {

    public bool TryDequeue( out TValue value )
    {
        value = (TValue)Dequeue();
        return true;
    }
}
