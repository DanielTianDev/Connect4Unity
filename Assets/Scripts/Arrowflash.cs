using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Arrowflash : MonoBehaviour {

    public GameObject arrow;
    public float flashTimer = 0.2f;
    public bool flash = false;

    private void Start()
    {
        if(flash)
        StartCoroutine(FlashArrow());
    }

    IEnumerator FlashArrow()
    {
        while (true)
        {
            arrow.SetActive(!arrow.activeSelf);

            yield return new WaitForSeconds(flashTimer);
        }
    }
}
