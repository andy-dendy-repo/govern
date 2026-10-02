// The bench's measurements of one board (Features/Design/Pages/BoardBenchPage at /design/board, Development only): the last
// scene applied, a full rebuild, frames while the camera pans and the triangles each layer draws. Read-only for the board: the
// camera ends where it was and the last scene's timings are kept.

// Measures a board; the numbers match Features/Design/Models/BoardBenchResult.cs.
export async function measure(board) {
    const applied = board.built ?? { layers: {}, total: 0 };
    board.rebuild('all');
    const rebuild = board.built.total;
    board.built = applied;
    const { target, distance } = { target: board.rig.target.clone(), distance: board.rig.distance };
    const samples = await panFrames(board, 60);
    const info = board.renderer.info.render;
    board.rig.target.copy(target);
    board.rig.distance = distance;
    board.rig.update();
    board.requestRender();
    const sorted = [...samples.frames].sort((a, b) => a - b);
    return {
        sceneBuild: 0,
        apply: applied.total,
        layers: applied.layers,
        rebuild,
        frame: average(samples.frames),
        frameP95: sorted[Math.floor(sorted.length * 0.95)] ?? 0,
        labels: average(samples.labels),
        draw: average(samples.draws),
        labelCount: board.labels.count,
        calls: info.calls,
        triangles: info.triangles,
        pixelRatio: board.renderer.getPixelRatio(),
        shadowMap: board.sun.shadow.mapSize.x,
        shares: triangleShares(board),
    };
}

const average = values => values.reduce((sum, value) => sum + value, 0) / Math.max(values.length, 1);

// Pans a little on each of `count` animation frames, drawing as a pan does; the times between frames include the browser's own
// work (styles, layout, compositing the labels).
function panFrames(board, count) {
    const samples = { frames: [], labels: [], draws: [] };
    return new Promise(resolve => {
        let last = null;
        const frame = time => {
            if (last !== null) {
                samples.frames.push(time - last);
            }
            last = time;
            if (samples.frames.length >= count || board.disposed) {
                resolve(samples);
                return;
            }
            const step = (samples.frames.length % 20 < 10 ? 1 : -1) * 0.02;
            board.rig.pan({ x: 0, y: 0 }, { x: step, y: step / 2 });
            const began = performance.now();
            board.labels.place(board.rig.camera, board.canvas.clientWidth, board.canvas.clientHeight);
            const placed = performance.now();
            board.renderer.render(board.world, board.rig.camera);
            samples.labels.push(placed - began);
            samples.draws.push(performance.now() - placed);
            requestAnimationFrame(frame);
        };
        requestAnimationFrame(frame);
    });
}

// Triangles each layer hands to the GPU per pass (instances counted), by layer name.
function triangleShares(board) {
    const layers = { terrain: board.terrain?.group, overlay: board.overlay };
    for (const child of board.layers?.children ?? []) {
        layers[child.userData.layer ?? 'other'] = child;
    }
    return Object.fromEntries(Object.entries(layers).map(([name, group]) => [name, triangles(group)]));
}

function triangles(group) {
    let count = 0;
    group?.traverse(object => {
        if (object.isMesh && object.visible) {
            const geometry = object.geometry;
            const vertices = geometry.index ? geometry.index.count : geometry.attributes.position.count;
            count += Math.floor(vertices / 3) * (object.isInstancedMesh ? object.count : 1);
        }
    });
    return count;
}
