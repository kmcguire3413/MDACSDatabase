class MDACSAnonFeedback extends React.Component {
    constructor(props) {
        super(props);

        this.state = {
            showFeedbackBox: false,
        }
    }

    render() {
        const state = this.state;
        const props = this.props;

        const onShowFeedbackClick = (e) => {
            e.preventDefault();

            this.setState({
                showFeedbackBox: true,
            })
        };

        if (state.showFeedbackBox) {
            const onSendFeedback = (e) => {
                this.setState({
                    showFeedbackBox: false,
                });

                let msg = {
                    feedback: state.feedbackValue,
                };

                request
                    .post(props.postUrl)
                    .send(JSON.stringify(msg))
                    .end((err, res) => {
                    });
            };

            const onCloseFeedback = (e) => {
                this.setState({
                    showFeedbackBox: false,
                });
            };

            const onFeedbackChange = (e) => {
                const value = e.target.value;

                this.setState({
                    feedbackValue: value,
                });
            };

            return <div className="static-modal">
                    <Modal.Dialog>
                        <Modal.Header>
                            <Modal.Title>Anonymous Feedback</Modal.Title>
                        </Modal.Header>
                        <Modal.Body>
                            Type your feedback:
                            <div>
                                <FormControl onChange={onFeedbackChange} componentClass="textarea" placeholder="Type your feedback here." />
                            </div>
                        </Modal.Body>
                        <Modal.Footer>
                            <Button onClick={onSendFeedback}>Send Feedback</Button>
                            <Button onClick={onCloseFeedback}>Cancel</Button>
                        </Modal.Footer>
                    </Modal.Dialog>
                </div>;
        }

        return <Button onClick={onShowFeedbackClick}>Give Anonymous Feedback</Button>;
    }
}