using UnityEngine;
using System;

public class GridClicker : MonoBehaviour {
    private GridNode node;
    private Action<GridNode> onClick;

    public void Setup(GridNode n, Action<GridNode> callback) {
        node = n;
        onClick = callback;
    }

    private void OnMouseDown() {
        onClick?.Invoke(node);
    }
}