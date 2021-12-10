using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DontDestroyTag : MonoBehaviour
{
    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }
}
