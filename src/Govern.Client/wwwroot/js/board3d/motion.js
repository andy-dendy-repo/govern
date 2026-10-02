// Animations on the 3D board: draws a SceneMotion (Features/Games/Models/SceneMotion.cs), which BoardAnimationService decides.
// A unit the motion moves becomes an actor (pieces.js buildActor) and glides towards each new place it is given, turned the
// way it goes, with a small hop per tile; when the motion lets it go it settles back on its tile, and once nothing moves the
// units return to the shared meshes. Ghosts walk or fade, damage numbers float (labels.js) and pulses grow on the tile top.
// What plays and when is never decided here; the durations come with the motion.
import { DoubleSide, Group, Mesh, MeshBasicMaterial, RingGeometry } from '../../lib/three/three.module.js';
import { buildActor, disposeActor } from './pieces.js';

// A move this long or longer (board units; neighbouring tiles are √3 apart) is a step to a tile; a shorter one is a lunge.
const STEP_DISTANCE = 1;
// How high a unit hops on each step, and how far a fading ghost sinks.
const HOP = 0.12;
const SINK = 0.2;
// A pulse: a flat hexagonal ring along the tile's edge that grows from PULSE_FROM to PULSE_TO times its size and fades.
const PULSE_FROM = 0.8;
const PULSE_TO = 1.15;
const ring = new RingGeometry(0.72, 0.95, 6).rotateX(-Math.PI / 2).rotateY(Math.PI / 6);

const ease = t => t < 0.5 ? 2 * t * t : 1 - Math.pow(-2 * t + 2, 2) / 2;

// The angle that turns a model's front (towards the camera, +z) to face along (dx, dz).
const facing = (dx, dz) => Math.atan2(dx, dz);

const TAU = Math.PI * 2;

// Turns from one angle towards another the short way round.
function turnBetween(from, to, t) {
    const delta = ((((to - from) % TAU) + TAU * 1.5) % TAU) - Math.PI;
    return from + delta * t;
}

// An angle within (-π, π].
const wrap = angle => Math.PI - ((((Math.PI - angle) % TAU) + TAU) % TAU);

export class Motion {
    constructor(board) {
        this.board = board;
        this.data = null;
        this.units = new Map();
        this.ghosts = new Map();
        this.effects = new Map();
        // Effects and fading ghosts that have played out: the service keeps them in its motion until the batch ends, and they
        // must not play again.
        this.expired = new Set();
        this.group = new Group();
        board.world.add(this.group);
        this.reduced = window.matchMedia('(prefers-reduced-motion: reduce)');
    }

    // The units whose pieces are drawn as actors.
    get moving() {
        return new Set(this.units.keys());
    }

    // Takes a new motion: retargets moving units, adds and removes ghosts and effects.
    set(data, now) {
        this.data = data;
        // An empty motion (a click, a newer view, or the end) puts every unit back on its tile at once.
        const finished = !data?.positions?.length && !data?.ghosts?.length && !data?.effects?.length;
        const keys = new Set([...(data?.ghosts ?? []).map(ghost => `g${ghost.key}`), ...(data?.effects ?? []).map(effect => `e${effect.key}`)]);
        for (const key of this.expired) {
            if (!keys.has(key)) {
                this.expired.delete(key);
            }
        }
        const positions = new Map((data?.positions ?? []).map(position => [position.unit, position]));
        for (const [unit, position] of positions) {
            const rest = this.board.pieceOf(unit);
            if (!rest) {
                continue;
            }
            const target = { x: position.x, y: position.height, z: position.z };
            let state = this.units.get(unit);
            if (!state) {
                // The view already has the unit where it ends up: a walk starts by drawing it a step or more away, on the tile it
                // starts from, at once; a lunge starts from its tile.
                state = this.start(rest);
                if (Math.hypot(target.x - rest.x, target.z - rest.z) >= STEP_DISTANCE) {
                    state = this.start({ x: target.x, height: target.y, z: target.z });
                }
                this.units.set(unit, state);
            }
            this.retarget(state, target, false, now);
        }
        for (const [unit, state] of this.units) {
            const rest = this.board.pieceOf(unit);
            if (!rest) {
                this.units.delete(unit);
            } else if (finished) {
                state.current = { x: rest.x, y: rest.height, z: rest.z, turn: 0 };
                state.to = { ...state.current };
                state.duration = 0;
                state.settling = true;
            } else if (!positions.has(unit) && !(state.settling && state.to.x === rest.x && state.to.z === rest.z)) {
                this.retarget(state, { x: rest.x, y: rest.height, z: rest.z }, true, now);
            }
        }
        const actors = this.board.pieces?.actors;
        if ([...this.units.keys()].some(unit => !actors?.has(unit))) {
            this.board.rebuildPieces();
        }

        const ghosts = new Map((data?.ghosts ?? []).map(ghost => [ghost.key, ghost]));
        for (const [key, ghost] of this.ghosts) {
            if (!ghosts.has(key)) {
                this.removeGhost(key, ghost);
            }
        }
        for (const [key, ghost] of ghosts) {
            if (this.expired.has(`g${key}`)) {
                continue;
            }
            const known = this.ghosts.get(key);
            const target = { x: ghost.piece.x, y: ghost.piece.height, z: ghost.piece.z };
            if (known) {
                this.retarget(known, target, false, now);
                continue;
            }
            const actor = buildActor(ghost.piece, this.board.palette(), ghost.fading);
            this.group.add(actor.group);
            const state = { ...this.start(ghost.piece), actor, fading: ghost.fading, born: now };
            this.ghosts.set(key, state);
        }

        const effects = new Map((data?.effects ?? []).map(effect => [effect.key, effect]));
        for (const [key, effect] of this.effects) {
            if (!effects.has(key)) {
                this.removeEffect(key, effect);
            }
        }
        for (const [key, effect] of effects) {
            if (!this.effects.has(key) && !this.expired.has(`e${key}`)) {
                this.effects.set(key, this.addEffect(key, effect, now));
            }
        }
    }

    // A unit's state, drawn where it stands.
    start(piece) {
        const at = { x: piece.x, y: piece.height, z: piece.z, turn: 0 };
        return { current: { ...at }, from: { ...at }, to: { ...at }, began: 0, duration: 0, hop: 0, settling: false };
    }

    // Starts a glide from where the piece is drawn to `to`: a step (with a hop) or a lunge, turned the way it goes; back on its
    // tile, it turns to face the camera again.
    retarget(state, to, rest, now) {
        const [dx, dz] = [to.x - state.current.x, to.z - state.current.z];
        const distance = Math.hypot(dx, dz);
        const step = distance >= STEP_DISTANCE;
        const turn = rest ? 0 : distance > 0.001 ? facing(dx, dz) : state.current.turn;
        if (distance < 0.001 && Math.abs(to.y - state.current.y) < 0.001 && Math.abs(turn - state.current.turn) < 0.001) {
            state.to = { ...state.current };
            state.settling = rest;
            return;
        }
        const timing = this.data ?? { step: 120, lunge: 160 };
        state.from = { ...state.current };
        state.to = { ...to, turn };
        state.began = now;
        state.duration = step || distance < 0.001 ? timing.step : timing.lunge / 2;
        state.hop = step ? HOP : 0;
        state.settling = rest;
    }

    // Moves a state along its glide; true while it is still on its way.
    advance(state, now) {
        const t = state.duration > 0 ? Math.min((now - state.began) / state.duration, 1) : 1;
        const eased = ease(t);
        const { from, to, current } = state;
        current.x = from.x + (to.x - from.x) * eased;
        current.z = from.z + (to.z - from.z) * eased;
        current.y = from.y + (to.y - from.y) * eased + state.hop * Math.sin(Math.PI * t);
        current.turn = t < 1 ? turnBetween(from.turn, to.turn, Math.min(t * 2, 1)) : wrap(to.turn);
        return t < 1;
    }

    // Places an actor's group and its banner where its state is drawn.
    place(group, state, piece) {
        group.position.set(state.current.x, state.current.y, state.current.z);
        group.rotation.y = state.current.turn;
        if (piece?.unit != null) {
            this.board.labels.follow(piece.unit, state.current.x - piece.x, state.current.y - piece.height, state.current.z - piece.z);
        }
    }

    // Puts the actors (built again after a rebuild) where they are drawn.
    reapply() {
        for (const [unit, state] of this.units) {
            const actor = this.board.pieces?.actors.get(unit);
            if (actor) {
                this.place(actor.group, state, actor.piece);
            }
        }
    }

    // Advances everything by one frame; true while anything still moves or shows.
    tick(now) {
        let running = false;
        for (const [unit, state] of this.units) {
            const moving = this.advance(state, now);
            const actor = this.board.pieces?.actors.get(unit);
            if (actor) {
                this.place(actor.group, state, actor.piece);
            }
            if (moving || !state.settling) {
                running = true;
            }
        }
        // Every unit is back on its tile and the motion holds none: they return to the shared meshes.
        if (this.units.size > 0 && !running) {
            for (const unit of this.units.keys()) {
                this.board.labels.follow(unit, 0, 0, 0);
            }
            this.units.clear();
            this.board.rebuildPieces();
        }

        const lifetime = this.data?.lifetime ?? 900;
        for (const [key, ghost] of this.ghosts) {
            running = this.advance(ghost, now) || running;
            if (ghost.fading) {
                const t = Math.min((now - ghost.born) / lifetime, 1);
                if (!this.reduced.matches) {
                    ghost.current.y -= SINK * t;
                }
                ghost.actor.materials.solid.opacity = ghost.actor.materials.painted.opacity = 1 - t;
                if (t >= 1) {
                    this.removeGhost(key, ghost);
                    this.expired.add(`g${key}`);
                    continue;
                }
            }
            this.place(ghost.actor.group, ghost, null);
            running = true;
        }

        for (const [key, effect] of this.effects) {
            const t = Math.min((now - effect.born) / lifetime, 1);
            if (t >= 1) {
                this.removeEffect(key, effect);
                this.expired.add(`e${key}`);
                continue;
            }
            if (effect.mesh) {
                const scale = this.reduced.matches ? 1 : PULSE_FROM + (PULSE_TO - PULSE_FROM) * ease(t);
                effect.mesh.scale.set(scale, 1, scale);
                effect.mesh.material.opacity = 1 - t;
            }
            running = true;
        }
        return running;
    }

    addEffect(key, effect, now) {
        if (effect.kind === 'damage') {
            this.board.labels.addDamage(key, effect.text ?? '', effect.x, effect.height, effect.z);
            return { born: now };
        }
        const material = new MeshBasicMaterial({
            color: this.board.palette().token('--gv-selected'), transparent: true, depthWrite: false, depthTest: false, side: DoubleSide,
        });
        // Drawn over everything on the tile (a city or a unit would hide it).
        const mesh = new Mesh(ring, material);
        mesh.renderOrder = 10;
        mesh.position.set(effect.x, effect.height + 0.06, effect.z);
        this.group.add(mesh);
        return { born: now, mesh };
    }

    removeEffect(key, effect) {
        if (effect.mesh) {
            this.group.remove(effect.mesh);
            effect.mesh.material.dispose();
        } else {
            this.board.labels.removeDamage(key);
        }
        this.effects.delete(key);
    }

    removeGhost(key, ghost) {
        this.group.remove(ghost.actor.group);
        disposeActor(ghost.actor);
        this.ghosts.delete(key);
    }

    dispose() {
        for (const [key, ghost] of this.ghosts) {
            this.removeGhost(key, ghost);
        }
        for (const [key, effect] of this.effects) {
            this.removeEffect(key, effect);
        }
        this.board.world.remove(this.group);
        ring.dispose();
    }
}
