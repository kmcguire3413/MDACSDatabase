

class MDACSDataItemDate extends React.Component {
    render() {
        return this.props.value;
    }
}

class MDACSDataItemTime extends React.Component {
    render() {
        return this.props.value;
    }
}

class MDACSDataItemUser extends React.Component {
    render() {
        return this.props.value;
    }
}

class MDACSDataItemDevice extends React.Component {
    render() {
        return this.props.value;
    }
}

class MDACSDataItemNote extends React.Component {
    render() {
        return this.props.value;
    }
}

class MDACSDataItemState extends React.Component {
    render() {
        return this.props.value;
    }
}

/// <summary>
/// Represents the data item. Handles the rendering of the data item and the controls for it.
/// </summary>
class MDACSDataItem extends React.Component {
    constructor(props) {
        super(props);

        this.state = {
            showEditBox: false,
            editTarget: null,
        };
    }

    render() {
        const state = this.state;
        const props = this.props;
        const setState = this.setState.bind(this);
        const item = props.item;

        return <tr>
                <td>{props.index}</td>
                <td><MDACSDataItemState value={item.state}/></td>
                <td><MDACSDataItemDate value={item.datestr}/></td>
                <td><MDACSDataItemTime value={item.timestr}/></td>
                <td><MDACSDataItemUser value={item.userstr}/></td>
                <td><MDACSDataItemDevice value={item.devicestr}/></td>
                <td style={{ width: '100%' }}><MDACSDataItemNote value={item.note}/></td>
            </tr>;
    }
}