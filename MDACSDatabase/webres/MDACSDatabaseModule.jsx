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

class MDACSDataItem extends React.Component {
    constructor(props) {
        super(props);
    }

    render() {
    }
}

class MDACSDataView extends React.Component {
    constructor(props) {
        super(props)
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

const MDACSDatabaseModuleStateGenerator = () => {
    return {
    };
};

const MDACSDatabaseModuleMutators = {
    onWorkerMessage: (props, state, setState, msg) => {
        // A worker message can be of these types:
        //
        //      RequestPageData
        //          This message contains the items for a single page
        //          of data and have already been presorted by the web
        //          worker.
        //
        //      NoteSaved
        //          Signals that the note for the item has been saved.
        //      StateSaved
        //          Signals that the state for the item has been saved.
        //      ItemDeleted
        //          Signals that an item has been removed and should be
        //          marked as deleted. My plans currently are to gray
        //          this item out in the listing.
    },
};

const MDACSDatabaseModuleViews = {
    Main: (props, state, setState, mutators) => {
        return <div>The database module is now loaded.</div>;
    },
};

/// <prop name="dao">DAO for the database service.</prop>
class MDACSDatabaseModule extends React.Component {
    constructor(props) {
        super(props);

        this.state = MDACSDatabaseModuleStateGenerator(props);
    }

    componentDidMount() {
        // We have to serialize the needed information so that the web worker
        // can create its own DAO (data access object). This allows the sharing
        // of the DAO implementation with the web worker.
        this.state.worker = new Worker('MDACSDataWorker.js');
        
        /*
            The worker not only moves CPU intensive work off the UI thread but
            also helps to encapsulate the complexity and create a kind of abstraction
            for dealing with the data. The worker maintains state about the currently
            viewed sub-set of the data and responds with messages. We can also send
            directives to the worker telling it what data we need and when to update
            its local cache of data.

                RequestPageData { startIndex: 0, endIndex: 100 }
                    Request a page of data by returning the items specified by the
                    inclusive minimum and exclusive maximum indexes.
                SaveNote { sid: 'the security id', note: 'the new note' }
                    Request that a note is saved. The worker will respond with the
                    success or failure of the operation.
                SaveState { sid: 'the security id', state: 'the new state' }
        */
        this.state.worker.postMessage({
            authUrl: this.props.dao.url_auth,
            dbUrl: this.props.dao.url_service,
            username: this.props.dao.username,
            hashedPassword: this.props.hashed_password,
        });

        this.state.workers.onmessage = (msg) => MDACSDatabaseModuleMutators.onWorkerMessage(
            this.props.bind(this), 
            this.state.bind(this), 
            this.setState.bind(this),
            msg
        ); 
    }

    componentWillUnmount() {
    }

    render() {
        return MDACSDatabaseModuleViews.Main(
            this.props,
            this.state,
            this.setState.bind(this),
            MDACSDatabaseModuleMutators
        );
    }
}