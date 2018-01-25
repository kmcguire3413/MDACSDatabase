

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
    constructor (props) {
        super(props);

        this.state = {
            value: props.value,
        }
    }

    render() {
        const props = this.props;
        const state = this.state;
        const setState = this.setState.bind(this);

        const changeNote = (e) => {
            e.preventDefault();

            let newNote = prompt("Note", state.value);

            if (newNote === null) {
                return;
            }

            setState({
                value: newNote,
            });

            props.updater.setNote(props.sid, newNote);
        };

        return  <span>
                    <Button bsSize="xsmall" bsStyle="link" onClick={changeNote}>Edit</Button>
                    {state.value}
                </span>;
    }
}

function inList(v, l) {
    for (let x = 0; x < l.length; ++x) {
        if (l[x] === v) {
            return true;
        }
    }

    return false;
}

function forEachItemSet(base, items, setTo) {
    for (var x = 0; x < items.length; ++x) {
        var items_value = items[x];

        base[items_value] = setTo;
    }
}

class MDACSDataItemState extends React.Component {
    constructor (props) {
        super(props);

        this.state = {
            value: props.value,
        };
    }

    render() {
        const props = this.props;
        const state = this.state;
        const setState = this.setState.bind(this);

        const listAutoPurge = [
            'auto-purge-30-days-before-delete-cloud-180-days',
            'auto-purge-90-days-before-delete-cloud-180-days',
            'auto-purge-180-days-before-delete-cloud-180-days',
        ];

        const listDeleted = [
            'deleted'
        ];
        
        const descName = {
            'auto-purge-30-days-before-delete-cloud-180-days': 'Auto-Purge 30/Cloud 180',
            'auto-purge-90-days-before-delete-cloud-180-days': 'Auto-Purge 90/Cloud 180',
            'auto-purge-180-days-before-delete-cloud-180-days': 'Auto-Purge 180/Cloud 180',
            'deleted': 'Deleted',
            'keep-forever': 'Keep Forever',
            'unknown': 'Unselected/New Arrival',
            '': 'Unselected/New Arrival',
            'delete': 'Schedule Delete',
            null: 'Unselected/New Arrival',
            undefined: 'Unselected/New Arrival',
            'restore': 'Restore',
        };

        const toStateFrom = {};

        const a = listAutoPurge.concat(['delete', 'keep-forever']);

        forEachItemSet(toStateFrom, listAutoPurge, a);
        forEachItemSet(toStateFrom, ['delete', 'keep-forever', 'unknown'], a);
        forEachItemSet(toStateFrom, ['', null, undefined], a);
        forEachItemSet(toStateFrom, ['deleted'], ['restore']);

        const options = [];

        const curState = state.value;
        const curFutureStates = toStateFrom[curState];

        if (curFutureStates !== null && curFutureStates !== undefined) {
            let curStateAdded = false;

            for (let x = 0; x < curFutureStates.length; ++x) {
                let v = curFutureStates[x];
                let vDescriptive = descName[v] ? descName[v] : null;

                if (v === curState) {
                    curStateAdded = true;
                }

                if (vDescriptive !== null) {
                    options.push(<option key={v} value={v}>{vDescriptive}</option>);
                }
            }

            let vDescriptive = descName[curState] ? descName[curState] : '?' + curState;

            if (curStateAdded === false) {
                options.push(<option key={curState} value={curState}>{vDescriptive}</option>);
            }
        }

        const onChange = (e) => {
            let newValue = e.target.value;
            props.updater.setState(props.sid, newValue);
            setState({
                value: newValue,
            });
        };

        return <select value={curState} onChange={onChange}>
            {options}
            </select>;
    }
}

/// <summary>
/// Represents the data item. Handles the rendering of the data item and the controls for it.
/// </summary>
/// <prop name="index">The descriptive index of the item within all items.</prop>
/// <prop name="item">The item object.</prop>
/// <prop name="updater">The object with callable methods to schedule changes.</prop>
/// <prop name="viewer">The object with callable methods to view items.</prop>
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
        const updater = props.updater;
        const viewer = props.viewer;

        const onViewClick = (e) => {
            e.preventDefault();

            viewer.viewItemStack(item);
        };

        const childrenCount = item.children ? item.children.length : 0;
        const dataType = item.datatype;

        return <tr>
                <td>{props.index}</td>
                <td><MDACSDataItemState value={item.state} sid={item.security_id} updater={updater}/></td>
                <td><MDACSDataItemDate value={item.datestr} sid={item.security_id} updater={updater}/></td>
                <td><MDACSDataItemTime value={item.timestr} sid={item.security_id} updater={updater}/></td>
                <td><MDACSDataItemUser value={item.userstr} sid={item.security_id} updater={updater}/></td>
                <td><MDACSDataItemDevice value={item.devicestr} sid={item.security_id} updater={updater}/></td>
                <td><Button bsSize="xsmall" bsStyle="link" onClick={onViewClick}>View [{dataType}:{childrenCount + 1}]</Button></td>
                <td style={{ width: '100%' }}><MDACSDataItemNote value={item.note} sid={item.security_id} updater={updater}/></td>
            </tr>;
    }
}