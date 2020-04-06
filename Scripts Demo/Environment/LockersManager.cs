using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LockersManager 
{
    // This class for Locker searching purposes for guards
    // Using list of lockers to guarantee the guard do not search the same locker
    /// <summary>
    /// Lockers to search list
    /// </summary>
    public List<DoorController> Lockers;

    /// <summary>
    /// The current locker being assigned to the current guard
    /// </summary>
    public DoorController CurrentLocker;

    /// <summary>
    /// Check if we initialized locker search
    /// </summary>
    public bool InitiateLockerSearching = false;

    /// <summary>
    /// Check how many lockers are being searched
    /// Different from total lockers available around needed to be searched
    /// For example, there are 3 lockers around the guards where they heard noise. NUmsearching will only be 2 because there are only 2 guards, which mean the 2 current searching lockers
    /// </summary>
    public int NumerSearchingLockers = 0;

    /// <summary>
    /// Default constructor
    /// </summary>
    public LockersManager()
    {
        Lockers = new List<DoorController>();
    }
}
