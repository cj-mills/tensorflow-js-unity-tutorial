
async function PerformInferenceAsync(model, float32Data) {

    const outputData = tf.tidy(() => {
        const shape = [1, this.height, this.width, 3];
        const input_tensor = tf.tensor(float32Data, shape, 'float32');
        // Make a prediction.
        return this.model.predict(input_tensor);
    });
    const output = await outputData.data();

    return output;
}

function argMax(array) {
    return array.map((x, i) => [x, i]).reduce((r, a) => (a[0] > r[0] ? a : r))[1];
}