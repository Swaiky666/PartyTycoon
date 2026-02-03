using UnityEngine;
using System;
public class ArrowButton : MonoBehaviour {
    public Action onClicked;
    private void OnMouseDown() { onClicked?.Invoke(); }
}