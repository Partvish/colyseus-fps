using UnityEngine;
using LucidSightTools;

public class Bullet : MonoBehaviour
{
    private string _ownerId;

    private Rigidbody _rigidbody;

    [SerializeField]
    private GameObject impactEffect = null;

    public void Fire(string owner, float speed, Vector3 direction)
    {
        _ownerId = owner;
        if (_rigidbody == null)
        {
            _rigidbody = GetComponent<Rigidbody>();
        }

        _rigidbody.velocity = direction * speed;
    }

    private void OnCollisionEnter(Collision collision)
    {
        LSLog.Log("Collision with type of: "+ collision.collider.name + "From: "+ _ownerId);
        GameObject impactFX = Instantiate(impactEffect, transform.position, Quaternion.identity);
        Destroy(impactFX, 1.0f);
        Destroy(gameObject);
        collision.gameObject.SendMessageUpwards("OnHit", _ownerId, SendMessageOptions.DontRequireReceiver);
    }
}
