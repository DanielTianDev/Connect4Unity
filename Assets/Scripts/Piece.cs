using UnityEngine;

public class Piece : MonoBehaviour {


    Vector3 GoalPosition;
    int tick = 0;

    public void SetCol(Vector3 goal)
    {
        GoalPosition = goal;
        GetComponent<Rigidbody>().useGravity = true;
    }

    private void Update()
    {
        if(Vector3.Distance(transform.position, GoalPosition) <= 0.5f || tick++>100)
        {
            GetComponent<Rigidbody>().isKinematic = true;
            GetComponent<Rigidbody>().useGravity = false;
            transform.position = GoalPosition;
            enabled = false;
        }
    }

}
