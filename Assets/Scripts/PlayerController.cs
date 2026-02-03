using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class PlayerController : MonoBehaviour {
    public GridNode currentGrid;
    public float moveSpeed = 5f;
    public GameObject arrowPrefab;
    public int playerId;
    private int lastInDir = -1;

    public void StartMove(int steps) { StartCoroutine(MoveRoutine(steps)); }

    private IEnumerator MoveRoutine(int steps) {
        int fuel = steps;
        while (fuel > 0) {
            List<int> available = new List<int>();
            int backDir = (lastInDir != -1) ? GridNode.GetOppositeDirection(lastInDir) : -1;
            for (int i = 0; i < 4; i++) {
                if (currentGrid.connections[i] != null && i != backDir) available.Add(i);
            }

            int chosen = -1;
            if (available.Count == 0) {
                if (backDir != -1 && currentGrid.connections[backDir] != null) chosen = backDir;
                else break;
            } else if (available.Count == 1) {
                chosen = available[0];
            } else {
                yield return StartCoroutine(ShowArrows(available, (res) => chosen = res));
            }

            if (chosen != -1) {
                currentGrid.RemovePlayer(gameObject);
                GridNode next = currentGrid.connections[chosen];
                lastInDir = chosen;
                Vector3 target = next.GetSlotPosition(gameObject);
                transform.LookAt(target);
                
                float t = 0;
                Vector3 start = transform.position;
                while (t < 1f) {
                    t += Time.deltaTime * moveSpeed;
                    transform.position = Vector3.Lerp(start, target, t);
                    yield return null;
                }
                currentGrid = next;
                fuel--;
            }
        }
    }

    private IEnumerator ShowArrows(List<int> dirs, System.Action<int> callback) {
        int selected = -1;
        List<GameObject> arrows = new List<GameObject>();
        foreach (int d in dirs) {
            GameObject a = Instantiate(arrowPrefab, transform.position + Vector3.up * 1f, Quaternion.Euler(0, d * 90f, 0));
            a.AddComponent<ArrowButton>().onClicked = () => selected = d;
            arrows.Add(a);
        }
        while (selected == -1) yield return null;
        foreach (var a in arrows) Destroy(a);
        callback(selected);
    }
}