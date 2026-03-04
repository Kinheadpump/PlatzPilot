using Microsoft.Maui.Controls;

namespace PlatzPilot.Behaviors;

public sealed class ShimmerBehavior : Behavior<VisualElement>
{
    private const string AnimationKey = "SkeletonShimmer";
    private VisualElement? _element;

    protected override void OnAttachedTo(VisualElement bindable)
    {
        base.OnAttachedTo(bindable);
        _element = bindable;
        StartAnimation();
    }

    protected override void OnDetachingFrom(VisualElement bindable)
    {
        bindable.AbortAnimation(AnimationKey);
        _element = null;
        base.OnDetachingFrom(bindable);
    }

    private void StartAnimation()
    {
        if (_element == null)
        {
            return;
        }

        var animation = new Animation(
            callback: value => _element.Opacity = 0.55 + (0.35 * value),
            start: 0,
            end: 1);

        animation.Commit(
            owner: _element,
            name: AnimationKey,
            rate: 16,
            length: 1200,
            easing: Easing.SinInOut,
            finished: null,
            repeat: () => true);
    }
}
