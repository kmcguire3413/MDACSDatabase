/*
      MDACSLoginApp
        Login
        LoggedIn
          // Controls what major panel is displayed.
          MDACSServiceDirectory
            AuthServicePanel
              *
            StorageJugglerPanel
              *
            DatabaseServicePanel
              DeviceConfigurationList
                DeviceConfiguration
              DataItemSearch
              DataItemList
                DataItem
                  DataItemDateTime
                  DataItemUser
                  DataItemDevice
                  DataItemNote
                  DataItemControls
*/

class MDACSDataViewSummary extends React.Component {
    constructor(props) {
        super(props);
    }

    render() {
    }
}

class MDACSDeviceConfiguration extends React.Component {
    constructor(props) {
        super(props);
    }

    render() {
    }
}

class MDACSConfigurationList extends React.Component {
    constructor(props) {
        super(props) 
    }

    render() {
    }
}

/// <prop name="updater">The object with callable methods for updating data.</prop>
/// <prop name="beginIndex">Only for visual purposes, if needed.</prop>
/// <prop name="endIndex">Only for visual purposes, if needed.</prop>
/// <prop name="data">Used to render the items. The entire data array is rendered.</prop>
class MDACSDataView extends React.Component {
    constructor(props) {
        super(props)
    }

    shouldComponentUpdate(nextProps, nextState) {
        let nextData = nextProps.data;  
        let curData = this.props.data;

        if (curData === null && nextData === null) {
            return false;
        }

        if (curData === null || nextData === null) {
            return true;
        }

        if (curData.length !== nextData.length) {
            return true;
        }

        for (let x = 0; x < curData.length; ++x) {
            if (curData[x] !== nextData[x]) {
                return true;
            }
        }

        return false;
    }

    render() {
        let props = this.props;
        let data = props.data;
        let beginIndex = props.beginIndex;
        let endIndex = props.endIndex;

        if (data === null) {
            return <div>No data.</div>;
        }

        let tmp = [];

        for (let x = 0; x < data.length; ++x) {
            let item = data[x];

            if (item !== undefined && item !== null) {
                console.log('item', x, item);
                tmp.push(<MDACSDataItem updater={props.updater} index={x + beginIndex} key={item.security_id} item={item}/>);
            }
        }

        let footer = null;

        if (tmp.length === 0) {
            footer = <div>No items from {beginIndex} to {endIndex - 1}.</div>;
        }

        return  <div style={{backgroundColor: 'white'}}>
                <Table striped bordered>
                    <thead>
                        <tr>
                            <td>Index</td>
                            <td>State</td>
                            <td>Date</td>
                            <td>Time</td>
                            <td>User</td>
                            <td>Device</td>
                            <td style={{width: '100%'}}>Note</td>
                        </tr>
                    </thead>
                    <tbody>
                        {tmp}
                    </tbody>
                </Table>
                {footer}
                </div>;
    }
}

const MDACSDatabaseModuleStateGenerator = (props) => {
    let state = {
        data: null,
        error: null,
        beginIndex: 0,
        endIndex: 50,
        visibleCount: 50,
        dataCount: 0,
        pendingOperationCount: 0,
        builtInitialSubset: false,
        workerStatus: 'Loading...',
        worker: new Worker('utility?MDACSDataWorker.js'),
        searchValue: '',
        toggleDeletedbuttonText: 'Show Deleted',
        showDeleted: false,
    };

    return state;
};

const MDACSDatabaseModuleMutators = {
    onWorkerMessage: (props, state, setState, e) => {
        let msg = e.data;

        switch (msg.topic) {
            case 'LoadDataStringDone':
            {
                setState({
                    builtInitialSubset: true,
                });

                state.worker.postMessage({
                    topic: 'ProduceSubSet',
                    criteria: [],
                    showDeleted: state.showDeleted,
                });
                break;
            }
            case 'ProduceSubSetDone':
            {
                setState({
                    beginIndex: 0,
                    endIndex: state.visibleCount,
                    dataCount: msg.count,
                });

                state.worker.postMessage({
                    topic: 'GetSubSetOfSubSet',
                    beginIndex: 0,
                    endIndex: state.visibleCount
                });
                break;
            }
            case 'GetSubSetOfSubSetDone':
            {
                setState({
                    data: msg.subset,
                });
                break;
            }
            case 'Status':
            {
                setState({
                    workerStatus: msg.status,
                });

                console.log('status', msg.status);
                break;
            }
        }
    },
    prevPage: (props, state, setState) => {
        setState((prevState, props) => {
            state.worker.postMessage({
                topic: 'GetSubSetOfSubSet',
                beginIndex: prevState.beginIndex - prevState.visibleCount,
                endIndex: prevState.endIndex - prevState.visibleCount,
            });

            return {
                beginIndex: prevState.beginIndex - prevState.visibleCount,
                endIndex: prevState.endIndex - prevState.visibleCount,
                data: null,
            }
        });
    },
    nextPage: (props, state, setState) => {
        setState((prevState, props) => {
            state.worker.postMessage({
                topic: 'GetSubSetOfSubSet',
                beginIndex: prevState.beginIndex + prevState.visibleCount,
                endIndex: prevState.endIndex + prevState.visibleCount,
            });

            return {
                beginIndex: prevState.beginIndex + prevState.visibleCount,
                endIndex: prevState.endIndex + prevState.visibleCount,
                data: null,
            }
        });
    },
    onSearchChange: (props, state, setState, e) => {
        e.preventDefault();

        setState({
            searchValue: e.target.value,
        });
    },
    onSearchClick: (props, state, setState, e) => {
        e.preventDefault();

        state.worker.postMessage({
            topic: 'ProduceSubSet',
            criteria: state.searchValue.split(' '),
            showDeleted: state.showDeleted,
        });
    },
    onReloadData: (props, state, setState, e) => {
        if (e !== null && e !== undefined) {
            e.preventDefault();
        }

        setState({
            error: null,
            data: null,
            workerStatus: 'Loading...',
        });

        props.dao.data_noparse(
            (dataString) => {
                //dataString = generate_sample_data(10000);

                setState({
                    error: null,
                    data: null,
                });

                state.worker.postMessage({
                    topic: 'LoadDataString',
                    dataString: dataString,
                });
            },
            (res) => {
                console.log('data fetch error', res);
                setState({
                    error: 'A problem happened when retrieving data from the server.',
                });
            }
        );
    },
    onToggleDeleted: (props, state, setState, e) => {
        if (state.toggleDeletedbuttonText === 'Show Deleted') {
            setState({
                toggleDeletedbuttonText: 'Hide Deleted',
                beginIndex: 0,
                endIndex: state.visibleCount, 
                showDeleted: true,
            })

            state.worker.postMessage({
                topic: 'ProduceSubSet',
                criteria: [],
                showDeleted: state.showDeleted,
            });
        } else {
            setState({
                toggleDeletedbuttonText: 'Show Deleted',
                beginIndex: 0,
                endIndex: state.visibleCount,
                showDeleted: false,
            })

            state.worker.postMessage({
                topic: 'ProduceSubSet',
                criteria: [],
                showDeleted: state.showDeleted,
            });
        }
    },
};

const MDACSDatabaseModuleViews = {
    Main: (props, state, setState, mutators, updaterInterface) => {
        let errorView;

        const prevPage = () => mutators.prevPage(props, state, setState);
        const nextPage = () => mutators.nextPage(props, state, setState);
        const onSearchChange = (e) => mutators.onSearchChange(props, state, setState, e);
        const onSearchClick = (e) => mutators.onSearchClick(props, state, setState, e);
        const reloadData = (e) => mutators.onReloadData(props, state, setState, e);
        const toggleDeleted = (e) => mutators.onToggleDeleted(props, state, setState, e);

        if (state.error !== null) {
            errorView = <Alert>{state.error}</Alert>;
        } else {
            errorView = null;
        }

        let bar = <Table>
                    <thead>
                        <tr>
                            <td></td>
                            <td style={{width: '100%'}}></td>
                            <td></td>
                        </tr>
                    </thead>
                    <tbody>
                        <tr>
                            <td>
                                <div style={{ margin: '0.2in' }}>
                                    <Button onClick={prevPage}>Previous Page</Button>
                                </div>
                                <div style={{ margin: '0.2in' }}>
                                    <Button onClick={nextPage}>Next Page</Button>
                                </div>
                            </td>
                            <td>
                                <div>
                                    <Button
                                        onClick={onSearchClick}>Search</Button>
                                    <input 
                                        onChange={onSearchChange}
                                        type="text"
                                        placeholder="Type search words here."
                                        value={state.searchValue}/>
                                </div>
                                <hr/>
                                <div>
                                    <strong>
                                        Showing {state.beginIndex + 1} to {state.endIndex} items out of {state.dataCount} items using the search string "{state.searchValue}".
                                    </strong>
                                </div>
                            </td>
                            <td>
                                <div>
                                    <Button onClick={reloadData}>Reload</Button>
                                </div>
                                <div>
                                    <Button onClick={toggleDeleted}>{state.toggleDeletedbuttonText}</Button>
                                </div>
                            </td>
                        </tr>
                    </tbody>
                </Table>;

        let statusHeader = null;

        if (state.pendingOperationCount > 0) {
            statusHeader = <span style={{ 
                border: '1px solid black', 
                margin: '2px', 
                padding: '10px', 
                backgroundColor: 'white', 
                position: 'fixed', 
                top: '0px' 
            }}>Pending Updates: {state.pendingOperationCount}</span>;
        }

        return <div>
            <Panel bsStyle="primary">
                <Panel.Heading>
                    <Panel.Title componentClass="h3">Data View and Edit</Panel.Title>
                </Panel.Heading>
                <Panel.Body>
            <div>
            {bar}
            </div>
            <h3>{state.workerStatus}</h3>
            <h3>{errorView}</h3>
            <MDACSDataView updater={updaterInterface} data={state.data} beginIndex={state.beginIndex} endIndex={state.endIndex} />
            <div>
            {bar}
            </div>
            </Panel.Body>
            </Panel>
            {statusHeader}
            </div>;
    },
};

function generate_sample_data(count) {
    function padleftzero(v) {
        if (v.length == 1) {
            return '0' + v;
        }

        return v;
    }

    function cf(l) {
        let m = Math.floor(Math.random() * l.length);

        return l[m];
    }

    let users = [
        'bob',
        'mark',
        'jill',
        'bill',
        'bryan',
        'josh',
        'kevin',
        'sue',
        'beth',
        'alicia',
        'jasmine',
    ];

    let devices = [
        'gopro3029',
        'bodycam45',
        'laptop293',
        'upload',
        'gopro3832',
        'bodycam86',
    ];

    let tmp = [];

    for (let x = 0; x < count; ++x) {
        let year = padleftzero(Math.floor(Math.random() * 2000 + 1900).toString());
        let month = padleftzero(Math.floor(Math.random() * 12 + 1).toString());
        let day = padleftzero(Math.floor(Math.random() * 25 + 1).toString());
        let hour = padleftzero(Math.floor(Math.random() * 24).toString());
        let minute = padleftzero(Math.floor(Math.random() * 60).toString());
        let second = padleftzero(Math.floor(Math.random() * 60).toString());

        let item = {
            datestr: year + month + day,
            timestr: hour + minute + second,
            userstr: cf(users),
            devicestr: cf(devices),
            note: '',
            security_id: x,
            state: 'Auto-Purge 90-Days Then Cloud 180-Days',
        };

        tmp.push(item);
    }

    tmp = {
        data: tmp,
    };

    return JSON.stringify(tmp);
}

/// <prop name="dao">DAO for the database service.</prop>
class MDACSDatabaseModule extends React.Component {
    constructor(props) {
        super(props);

        this.state = MDACSDatabaseModuleStateGenerator(props);

        this.state.worker.onmessage = 
            (e) => {
                MDACSDatabaseModuleMutators.onWorkerMessage(
                    props,
                    this.state,
                    this.setState.bind(this),
                    e
                );
            };
    }

    setItemNote(sid, newNote) {
        this.setState((prev, props) => {
            return { pendingOperationCount: prev.pendingOperationCount + 1 };
        });

        this.props.dao.setNote(
            sid, 
            newNote, 
            () => {
                this.setState((prev, props) => {
                    return { pendingOperationCount: prev.pendingOperationCount - 1 };
                });
            },
            () => {
                this.setState((prev, props) => {
                    return { pendingOperationCount: prev.pendingOperationCount - 1 };
                });
            }
        );
    }

    setItemState(sid, newState) {
        this.setState((prev, props) => {
            return { pendingOperationCount: prev.pendingOperationCount + 1 };
        });

        this.props.dao.setState(
            sid, 
            newState,
            () => {
                this.setState((prev, props) => {
                    return { pendingOperationCount: prev.pendingOperationCount - 1 };
                });
            },
            () => {
                this.setState((prev, props) => {
                    return { pendingOperationCount: prev.pendingOperationCount - 1 };
                });
            }
        );
    }

    componentDidMount() {
        console.log('fetching data');
        
        MDACSDatabaseModuleMutators.onReloadData(
            this.props,
            this.state,
            this.setState.bind(this),
            null
        );
    }

    componentWillUnmount() {
    }

    render() {
        // More indirection. But, it does form an interface and that keeps anyone
        // in the future from having a direct reference to `this` and relying 
        // on functionality not intended to be exported as part of the updater
        // interface.
        let updaterInterface = {
            setNote: this.setItemNote.bind(this),
            setState: this.setItemState.bind(this),
        }

        return MDACSDatabaseModuleViews.Main(
            this.props,
            this.state,
            this.setState.bind(this),
            MDACSDatabaseModuleMutators,
            updaterInterface
        );
    }
}