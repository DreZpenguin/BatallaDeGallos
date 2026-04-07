using System.Collections;
using UnityEngine;

public class DeadZone : MonoBehaviour
{
    [SerializeField] private CircleCollider2D Zone;
    private Coroutine cuentaRegresiva;

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            if (cuentaRegresiva == null)
            {
                cuentaRegresiva = StartCoroutine(CuentaRegresivaPerder());
            }
        }
    }
    private void OnTriggerEnter2D(Collider2D other)
    {
        if(other.CompareTag("Player"))
        {
            if (cuentaRegresiva != null)
            {
                StopCoroutine(cuentaRegresiva);
                cuentaRegresiva = null;

                Debug.Log("volviste a tiempo");
            }
        }
    }
    IEnumerator CuentaRegresivaPerder()
    {
        int tiempo = 3;

        while (tiempo > 0)
        {
            Debug.Log("Tienes " + tiempo + " segundos para volver");
            yield return new WaitForSeconds(1f);
            tiempo--;
        }
        Debug.Log("Perdiste");
    }
}
