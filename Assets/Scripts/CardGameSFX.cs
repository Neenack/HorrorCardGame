using Unity.Netcode;
using UnityEngine;

public class CardGameSFX : NetworkBehaviour
{
    [SerializeField] private SoundLibrarySO playingCardSounds;
    [SerializeField] private ICardGameEvents game;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        game = GetComponent<ICardGameEvents>();

        if (game == null || !IsServer) return;

        game.OnAnyCardDrawn += Game_OnAnyCardDrawn;
        game.OnAnyCardPlacedOnPile += Game_OnAnyCardPlacedOnPile;
    }

    public override void OnNetworkDespawn()
    {
        game.OnAnyCardDrawn -= Game_OnAnyCardDrawn;
        game.OnAnyCardPlacedOnPile -= Game_OnAnyCardPlacedOnPile;
    }

    private void Game_OnAnyCardDrawn()
    {
        SoundFXManager.PlaySoundServer("CardDeal", transform.position);
    }

    private void Game_OnAnyCardPlacedOnPile()
    {
        //SoundFXManager.PlaySoundServer("CardSetDown", transform.position);
    }
}
