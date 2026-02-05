import { ref, watch, onUnmounted } from 'vue';
import type { Player } from '@/types/server-status';

interface InterpolatedPlayer extends Player {
    currentX: number;
    currentY: number;
    currentYaw: number;
}

export function usePlayerInterpolation(playersSource: () => Player[]) {
    const interpolatedPlayers = ref<InterpolatedPlayer[]>([]);
    let animationFrameId: number | null = null;
    let lastTime = performance.now();

    // Store target states for each player
    const targets = new Map<string, {
        startX: number;
        startY: number;
        startYaw: number;
        targetX: number;
        targetY: number;
        targetYaw: number;
        startTime: number;
        duration: number;
    }>();

    // Update targets when source players change
    watch(playersSource, (newPlayers) => {
        const now = performance.now();

        newPlayers.forEach(player => {
            const existing = interpolatedPlayers.value.find(p => p.uid === player.uid);

            if (existing) {
                // Update target for existing player
                targets.set(player.uid, {
                    startX: existing.currentX,
                    startY: existing.currentY,
                    startYaw: existing.currentYaw,
                    targetX: player.x,
                    targetY: player.y,
                    targetYaw: player.yaw,
                    startTime: now,
                    duration: 150 // Match server update rate (100ms) + buffer
                });
            } else {
                // New player, add immediately
                interpolatedPlayers.value.push({
                    ...player,
                    currentX: player.x,
                    currentY: player.y,
                    currentYaw: player.yaw
                });

                targets.set(player.uid, {
                    startX: player.x,
                    startY: player.y,
                    startYaw: player.yaw,
                    targetX: player.x,
                    targetY: player.y,
                    targetYaw: player.yaw,
                    startTime: now,
                    duration: 0
                });
            }
        });

        // Remove players that are no longer present
        interpolatedPlayers.value = interpolatedPlayers.value.filter(p =>
            newPlayers.some(np => np.uid === p.uid)
        );
    }, { deep: true });

    function lerp(start: number, end: number, t: number): number {
        return start + (end - start) * t;
    }

    function lerpAngle(start: number, end: number, t: number): number {
        let diff = end - start;
        // Normalize diff to -PI to PI (or -180 to 180 depending on unit, assuming radians here as per VS API)
        // VS usually uses radians for Yaw.
        while (diff > Math.PI) diff -= Math.PI * 2;
        while (diff < -Math.PI) diff += Math.PI * 2;
        return start + diff * t;
    }

    function animate() {
        const now = performance.now();
        // const dt = now - lastTime; // Not strictly needed for simple lerp based on start time
        lastTime = now;

        interpolatedPlayers.value.forEach(player => {
            const target = targets.get(player.uid);
            if (!target) return;

            const elapsed = now - target.startTime;
            let t = Math.min(1, elapsed / target.duration);

            // Smooth step for nicer movement
            // t = t * t * (3 - 2 * t); 

            if (target.duration === 0) t = 1;

            player.currentX = lerp(target.startX, target.targetX, t);
            player.currentY = lerp(target.startY, target.targetY, t);
            player.currentYaw = lerpAngle(target.startYaw, target.targetYaw, t);

            // Update base properties in case they changed (name, etc)
            const sourcePlayer = playersSource().find(p => p.uid === player.uid);
            if (sourcePlayer) {
                player.name = sourcePlayer.name;
                // ... other props
            }
        });

        animationFrameId = requestAnimationFrame(animate);
    }

    // Start animation loop
    animationFrameId = requestAnimationFrame(animate);

    onUnmounted(() => {
        if (animationFrameId !== null) {
            cancelAnimationFrame(animationFrameId);
        }
    });

    return {
        interpolatedPlayers
    };
}
