using UnityEngine;

public class EnemySword : MonoBehaviour
{
    private Instrument _instrument;
    private GameObject _instrumentView;
    private Collider _collider;
    public void Initialize(Instrument instrument, Quaternion offset)
    {
        _instrument = instrument;
        _collider = GetComponent<Collider>();
        SetInstrumentView(_instrument.InstrumentView, offset);
    }

    public void EnableCollide()
    {
        _collider.enabled = true;
    }

    public void DisableCollide()
    {
        _collider.enabled = false;
    }
    
    private void OnTriggerEnter(Collider other) 
    {
        if(other.TryGetComponent(out Player player))
        {
            HitPlayer(player);
        }
    }

    private void HitPlayer(Player player)
    {
        player.TakeDamage(_instrument.DamagePerHit);
    }

    private void SetInstrumentView(GameObject instrumentObj, Quaternion offset)
    {
        _instrumentView = Instantiate(instrumentObj, transform);
        _instrumentView.transform.localPosition = Vector3.zero;
        _instrumentView.transform.localRotation = offset;
    }
}
