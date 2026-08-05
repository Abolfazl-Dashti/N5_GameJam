using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GoalZone : MonoBehaviour
{
    [Header("VFX & UI")]
    [SerializeField] private ParticleSystem goalParticle;
    [SerializeField] private ParticleSystem goalParticle1;
    [SerializeField] private TMP_Text scoreText;  // تکست UI که شمارنده رو نشون میده
    
    [Header("Goal Settings")]
    [SerializeField] private LayerMask discLayer;

    private int _score;
    
    private void PlayGoalEffect()
    {
        if (goalParticle) goalParticle.Play();
        if (goalParticle1) goalParticle1.Play();
    }
    
    private void OnTriggerEnter(Collider other)
    {
        // چک میکنیم ابجکت ورودی لایه disc داره یا نه
        if ((discLayer.value & (1 << other.gameObject.layer)) == 0) return;
        
        PlayGoalEffect();
    
        // _score++;
        // scoreText.text = _score.ToString();
    }
}

